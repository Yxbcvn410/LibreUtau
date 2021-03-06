﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.IO;
using LibreUtau.Core.USTx;

namespace LibreUtau.Core.Formats {
    static class VSQx {
        public const string vsq3NameSpace = @"http://www.yamaha.co.jp/vocaloid/schema/vsq3/";
        public const string vsq4NameSpace = @"http://www.yamaha.co.jp/vocaloid/schema/vsq4/";

        public static UProject Load(string file) {
            XmlDocument vsqx = new XmlDocument();

            try {
                vsqx.Load(file);
            } catch (Exception e) {
                System.Windows.MessageBox.Show(e.GetType().ToString() + "\n" + e.Message);
                return null;
            }

            XmlNamespaceManager nsmanager = new XmlNamespaceManager(vsqx.NameTable);
            nsmanager.AddNamespace("v3", vsq3NameSpace);
            nsmanager.AddNamespace("v4", vsq4NameSpace);

            XmlNode root;
            string nsPrefix;

            // Detect vsqx version
            root = vsqx.SelectSingleNode("v3:vsq3", nsmanager);

            if (root != null) nsPrefix = "v3:";
            else {
                root = vsqx.SelectSingleNode("v4:vsq4", nsmanager);

                if (root != null) nsPrefix = "v4:";
                else {
                    System.Windows.MessageBox.Show("Unrecognizable VSQx file format.");
                    return null;
                }
            }

            UProject uproject = new UProject();
            uproject.RegisterExpression(new IntExpression(null, "velocity", "VEL") {Data = 64, Min = 0, Max = 127});
            uproject.RegisterExpression(new IntExpression(null, "volume", "VOL") {Data = 100, Min = 0, Max = 200});
            uproject.RegisterExpression(new IntExpression(null, "opening", "OPE") {Data = 127, Min = 0, Max = 127});
            uproject.RegisterExpression(new IntExpression(null, "accent", "ACC") {Data = 50, Min = 0, Max = 100});
            uproject.RegisterExpression(new IntExpression(null, "decay", "DEC") {Data = 50, Min = 0, Max = 100});

            string bpmPath = $"{nsPrefix}masterTrack/{nsPrefix}tempo/{nsPrefix}{(nsPrefix == "v3:" ? "bpm" : "v")}";
            string beatperbarPath =
                $"{nsPrefix}masterTrack/{nsPrefix}timeSig/{nsPrefix}{(nsPrefix == "v3:" ? "nume" : "nu")}";
            string beatunitPath =
                $"{nsPrefix}masterTrack/{nsPrefix}timeSig/{nsPrefix}{(nsPrefix == "v3:" ? "denomi" : "de")}";
            string premeasurePath = $"{nsPrefix}masterTrack/{nsPrefix}preMeasure";
            string resolutionPath = $"{nsPrefix}masterTrack/{nsPrefix}resolution";
            string projectnamePath = $"{nsPrefix}masterTrack/{nsPrefix}seqName";
            string projectcommentPath = $"{nsPrefix}masterTrack/{nsPrefix}comment";
            string trackPath = $"{nsPrefix}vsTrack";
            string tracknamePath = $"{nsPrefix}{(nsPrefix == "v3:" ? "trackName" : "name")}";
            string trackcommentPath = $"{nsPrefix}comment";
            string tracknoPath = $"{nsPrefix}{(nsPrefix == "v3:" ? "vsTrackNo" : "tNo")}";
            string partPath = $"{nsPrefix}{(nsPrefix == "v3:" ? "musicalPart" : "vsPart")}";
            string partnamePath = $"{nsPrefix}{(nsPrefix == "v3:" ? "partName" : "name")}";
            string partcommentPath = $"{nsPrefix}comment";
            string notePath = $"{nsPrefix}note";
            string postickPath = $"{nsPrefix}{(nsPrefix == "v3:" ? "posTick" : "t")}";
            string durtickPath = $"{nsPrefix}{(nsPrefix == "v3:" ? "durTick" : "dur")}";
            string notenumPath = $"{nsPrefix}{(nsPrefix == "v3:" ? "noteNum" : "n")}";
            string velocityPath = $"{nsPrefix}{(nsPrefix == "v3:" ? "velocity" : "v")}";
            string lyricPath = $"{nsPrefix}{(nsPrefix == "v3:" ? "lyric" : "y")}";
            string phonemePath = $"{nsPrefix}{(nsPrefix == "v3:" ? "phnms" : "p")}";
            string playtimePath = $"{nsPrefix}playTime";
            string partstyleattrPath =
                $"{nsPrefix}{(nsPrefix == "v3:" ? "partStyle" : "pStyle")}/{nsPrefix}{(nsPrefix == "v3:" ? "attr" : "v")}";
            string notestyleattrPath =
                $"{nsPrefix}{(nsPrefix == "v3:" ? "noteStyle" : "nStyle")}/{nsPrefix}{(nsPrefix == "v3:" ? "attr" : "v")}";

            uproject.BPM = Convert.ToDouble(root.SelectSingleNode(bpmPath, nsmanager).InnerText) / 100;
            uproject.BeatPerBar = int.Parse(root.SelectSingleNode(beatperbarPath, nsmanager).InnerText);
            uproject.BeatUnit = int.Parse(root.SelectSingleNode(beatunitPath, nsmanager).InnerText);
            uproject.Resolution = int.Parse(root.SelectSingleNode(resolutionPath, nsmanager).InnerText);
            uproject.FilePath = file;
            uproject.Name = root.SelectSingleNode(projectnamePath, nsmanager).InnerText;
            uproject.Comment = root.SelectSingleNode(projectcommentPath, nsmanager).InnerText;

            int preMeasure = int.Parse(root.SelectSingleNode(premeasurePath, nsmanager).InnerText);
            int partPosTickShift = -preMeasure * uproject.Resolution * uproject.BeatPerBar * 4 / uproject.BeatUnit;

            USinger usinger = new USinger();

            foreach (XmlNode track in root.SelectNodes(trackPath, nsmanager)) // track
            {
                UTrack utrack = new UTrack() {Singer = usinger, TrackNo = uproject.Tracks.Count};
                uproject.Tracks.Add(utrack);

                utrack.Name = track.SelectSingleNode(tracknamePath, nsmanager).InnerText;
                utrack.Comment = track.SelectSingleNode(trackcommentPath, nsmanager).InnerText;
                utrack.TrackNo = int.Parse(track.SelectSingleNode(tracknoPath, nsmanager).InnerText);

                foreach (XmlNode part in track.SelectNodes(partPath, nsmanager)) // musical part
                {
                    UVoicePart upart = new UVoicePart();
                    uproject.Parts.Add(upart);

                    upart.Name = part.SelectSingleNode(partnamePath, nsmanager).InnerText;
                    upart.Comment = part.SelectSingleNode(partcommentPath, nsmanager).InnerText;
                    upart.PosTick = int.Parse(part.SelectSingleNode(postickPath, nsmanager).InnerText) +
                                    partPosTickShift;
                    upart.DurTick = int.Parse(part.SelectSingleNode(playtimePath, nsmanager).InnerText);
                    upart.TrackNo = utrack.TrackNo;

                    foreach (XmlNode note in part.SelectNodes(notePath, nsmanager)) {
                        UNote unote = uproject.CreateNote();

                        unote.PosTick = int.Parse(note.SelectSingleNode(postickPath, nsmanager).InnerText);
                        unote.DurTick = int.Parse(note.SelectSingleNode(durtickPath, nsmanager).InnerText);
                        unote.NoteNum = int.Parse(note.SelectSingleNode(notenumPath, nsmanager).InnerText);
                        unote.Lyric = note.SelectSingleNode(lyricPath, nsmanager).InnerText;
                        unote.Phoneme.PhonemeString = note.SelectSingleNode(phonemePath, nsmanager).InnerText;

                        unote.Expressions["velocity"].Data =
                            int.Parse(note.SelectSingleNode(velocityPath, nsmanager).InnerText);

                        foreach (XmlNode notestyle in note.SelectNodes(notestyleattrPath, nsmanager)) {
                            if (notestyle.Attributes["id"].Value == "opening")
                                unote.Expressions["opening"].Data = int.Parse(notestyle.InnerText);
                            else if (notestyle.Attributes["id"].Value == "accent")
                                unote.Expressions["accent"].Data = int.Parse(notestyle.InnerText);
                            else if (notestyle.Attributes["id"].Value == "decay")
                                unote.Expressions["decay"].Data = int.Parse(notestyle.InnerText);
                        }

                        unote.PitchBend.Points[0].X = -uproject.TickToMillisecond(Math.Min(15, unote.DurTick / 3));
                        unote.PitchBend.Points[1].X = -unote.PitchBend.Points[0].X;
                        upart.Notes.Add(unote);
                    }
                }
            }

            return uproject;
        }
    }
}
