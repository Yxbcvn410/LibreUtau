﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using LibreUtau.Core.Commands;
using LibreUtau.Core.USTx;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace LibreUtau.Core.Formats {
    static class Wave {
        public static UWavePart CreatePart(string filepath) {
            foreach (var part in CommandDispatcher.Inst.Project.Parts) {
                var _part = part as UWavePart;
                if (_part != null && _part.FilePath == filepath) {
                    return new UWavePart {
                        FilePath = filepath,
                        FileDurTick = _part.FileDurTick,
                        DurTick = _part.DurTick,
                        Channels = _part.Channels,
                        Peaks = _part.Peaks
                    };
                }
            }

            WaveStream stream = null;
            try {
                stream = new AudioFileReader(filepath);
            } catch {
                return null;
            }

            int durTick =
                CommandDispatcher.Inst.Project.MillisecondToTick(1000.0 * stream.Length /
                                                                 stream.WaveFormat.AverageBytesPerSecond);
            UWavePart uwavepart = new UWavePart {
                FilePath = filepath,
                FileDurTick = durTick,
                DurTick = durTick,
                Channels = stream.WaveFormat.Channels
            };
            stream.Close();
            return uwavepart;
        }

        public static float[] BuildPeaks(UWavePart part, BackgroundWorker worker) {
            const double peaksRate = 4000;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            float[] peaks;
            using (var stream = new AudioFileReader(part.FilePath)) {
                int channels = part.Channels;
                double peaksSamples = (int)((double)stream.Length / stream.WaveFormat.BlockAlign /
                    stream.WaveFormat.SampleRate * peaksRate);
                peaks = new float[(int)(peaksSamples + 1) * channels];
                double blocksPerPixel = stream.Length / stream.WaveFormat.BlockAlign / peaksSamples;

                var converted = new WaveToSampleProvider(stream);

                float[] buffer = new float[4096];

                int readed;
                int readPos = 0;
                int peaksPos = 0;
                double bufferPos = 0;
                float lmax = 0, lmin = 0, rmax = 0, rmin = 0;
                while ((readed = converted.Read(buffer, 0, 4096)) != 0) {
                    readPos += readed;
                    for (int i = 0; i < readed; i += channels) {
                        lmax = Math.Max(lmax, buffer[i]);
                        lmin = Math.Min(lmin, buffer[i]);
                        if (channels > 1) {
                            rmax = Math.Max(rmax, buffer[i + 1]);
                            rmin = Math.Min(rmin, buffer[i + 1]);
                        }

                        if (i > bufferPos) {
                            lmax = -lmax;
                            lmin = -lmin;
                            rmax = -rmax;
                            rmin = -rmin; // negate peaks to fipped waveform
                            peaks[peaksPos * channels] = lmax == 0 ? lmin : lmin == 0 ? lmax : (lmin + lmax) / 2;
                            peaks[peaksPos * channels + 1] = rmax == 0 ? rmin : rmin == 0 ? rmax : (rmin + rmax) / 2;
                            peaksPos++;
                            lmax = lmin = rmax = rmin = 0;
                            bufferPos += blocksPerPixel * stream.WaveFormat.Channels;
                        }
                    }

                    bufferPos -= readed;
                    worker.ReportProgress((int)((double)readPos * sizeof(float) * 100 / stream.Length));
                }
            }

            sw.Stop();
            Debug.WriteLine("Build peaks {0} ms", sw.Elapsed.TotalMilliseconds);
            return peaks;
        }
    }
}
