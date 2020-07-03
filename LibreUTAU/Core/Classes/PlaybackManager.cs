﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using LibreUtau.Core.Render;
using LibreUtau.Core.ResamplerDriver;
using LibreUtau.Core.USTx;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace LibreUtau.Core {
    class PlaybackManager : ICmdSubscriber {
        MixingSampleProvider masterMix;
        private WaveOut outDevice;
        public int playbackPositionTick;

        List<TrackSampleProvider> trackSources;

        public bool CheckResampler() {
            var path = PathManager.Inst.GetPreviewEnginePath();
            Directory.CreateDirectory(PathManager.Inst.GetEngineSearchPath());
            return File.Exists(path); // TODO: validate exe / dll
        }

        public void Play(UProject project) {
            if (outDevice != null) {
                if (outDevice.PlaybackState == PlaybackState.Playing) return;
                if (outDevice.PlaybackState == PlaybackState.Paused) {
                    outDevice.Play();
                    return;
                }

                outDevice.Dispose();
            }

            ProjectBuilder builder = new ProjectBuilder(project);
            builder.Start(tracks => StartPlayback(tracks));
        }

        public void StopPlayback() {
            if (outDevice != null) outDevice.Stop();
        }

        public void PausePlayback() {
            if (outDevice != null) outDevice.Pause();
        }

        private void StartPlayback(List<TrackSampleProvider> trackSources, int deviceNumber = -1) {
            masterMix = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            foreach (var source in trackSources)
                masterMix.AddMixerInput(source);
            outDevice = new WaveOut {
                DeviceNumber = deviceNumber,
                NumberOfBuffers = 4
            };
            outDevice.Init(masterMix);
            outDevice.Play();
        }

        public void UpdatePlayPos() {
            if (outDevice != null && outDevice.PlaybackState == PlaybackState.Playing) {
                double ms = outDevice.GetPosition() * 1000.0 / masterMix.WaveFormat.BitsPerSample /
                    masterMix.WaveFormat.Channels * 8 / masterMix.WaveFormat.SampleRate;
                int tick = DocManager.Inst.Project.MillisecondToTick(ms);
                DocManager.Inst.ExecuteCmd(new SetPlayPosTickNotification(tick), true);
            }
        }

        private float DecibelToVolume(double db) {
            return (db == -24)
                ? 0
                : (float)((db < -16) ? MusicMath.DecibelToLinear(db * 2 + 16) : MusicMath.DecibelToLinear(db));
            //TODO Что за костыль?
        }

        #region Singleton

        private PlaybackManager() { this.Subscribe(DocManager.Inst); }

        private static PlaybackManager _s;

        public static PlaybackManager Inst {
            get {
                if (_s == null) { _s = new PlaybackManager(); }

                return _s;
            }
        }

        #endregion

        # region ICmdSubscriber

        public void Subscribe(ICmdPublisher publisher) {
            if (publisher != null) publisher.Subscribe(this);
        }

        public void OnCommandExecuted(UCommand cmd, bool isUndo) {
            if (cmd is SeekPlayPosTickNotification) {
                StopPlayback();
                int tick = ((SeekPlayPosTickNotification)cmd).playPosTick;
                DocManager.Inst.ExecuteCmd(new SetPlayPosTickNotification(tick));
            } else if (cmd is VolumeChangeNotification) {
                var _cmd = cmd as VolumeChangeNotification;
                if (trackSources != null && trackSources.Count > _cmd.TrackNo) {
                    trackSources[_cmd.TrackNo].Volume = DecibelToVolume(_cmd.Volume);
                }
            }
        }

        # endregion
    }
}
