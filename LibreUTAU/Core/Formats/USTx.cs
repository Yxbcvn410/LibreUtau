﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using LibreUtau.Core.USTx;
using LibreUtau.SimpleHelpers;

namespace LibreUtau.Core.Formats {
    class USTx {
        private const string ThisUstxVersion = "0.2";
        static UProject Project;

        public static UProject Create() {
            UProject project = new UProject {Saved = false};
            project.RegisterExpression(new IntExpression(null, "velocity", "VEL") {Data = 100, Min = 0, Max = 200});
            project.RegisterExpression(new IntExpression(null, "volume", "VOL") {Data = 100, Min = 0, Max = 200});
            project.RegisterExpression(new IntExpression(null, "gender", "GEN") {Data = 0, Min = -100, Max = 100});
            project.RegisterExpression(new IntExpression(null, "lowpass", "LPF") {Data = 0, Min = 0, Max = 100});
            project.RegisterExpression(new IntExpression(null, "highpass", "HPF") {Data = 0, Min = 0, Max = 100});
            project.RegisterExpression(new IntExpression(null, "accent", "ACC") {Data = 100, Min = 0, Max = 200});
            project.RegisterExpression(new IntExpression(null, "decay", "DEC") {Data = 0, Min = 0, Max = 100});
            return project;
        }

        public static void Save(string file, UProject project) {
            JavaScriptSerializer jss = new JavaScriptSerializer();
            jss.RegisterConverters(
                new List<JavaScriptConverter> {
                    new UProjectConvertor(),
                    new MiscConvertor(),
                    new UPartConvertor(),
                    new UNoteConvertor(),
                    new UPhonemeConverter()
                });
            StringBuilder str = new StringBuilder();
            try {
                jss.Serialize(project, str);
                var f_out = new StreamWriter(file);
                f_out.Write(str.ToString());
                f_out.Close();
                project.Saved = true;
                project.FilePath = file;
            } catch (Exception e) {
                Debug.WriteLine(e.ToString());
            }
        }

        public static UProject Load(string file) {
            UProject project;

            JavaScriptSerializer jss = new JavaScriptSerializer();
            jss.RegisterConverters(
                new List<JavaScriptConverter> {
                    new UProjectConvertor(),
                    new MiscConvertor(),
                    new UPartConvertor(),
                    new UNoteConvertor(),
                    new UPhonemeConverter()
                });

            try {
                project = jss.Deserialize(File.ReadAllText(file), typeof(UProject)) as UProject;
                project.Saved = true;
                project.FilePath = file;
            } catch (Exception e) {
                Debug.WriteLine(e.ToString());
                return null;
            }

            // Load singers
            foreach (var track in project.Tracks) {
                track.Singer = UtauSoundbank.GetSinger(track.Singer.Path, FileEncoding.DetectFileEncoding(file));
            }

            return project;
        }

        class UNoteConvertor : JavaScriptConverter {
            public override IEnumerable<Type> SupportedTypes {
                get { return new List<Type>(new[] {typeof(UNote)}); }
            }

            public override object Deserialize(IDictionary<string, object> dictionary, Type type,
                JavaScriptSerializer serializer) {
                UNote result = Project.CreateNote();
                result.Lyric = dictionary["y"] as string;
                result.NoteNum = Convert.ToInt32(dictionary["n"]);
                result.PosTick = Convert.ToInt32(dictionary["pos"]);
                result.DurTick = Convert.ToInt32(dictionary["dur"]);
                result.PitchBend.SnapFirst = Convert.ToBoolean(dictionary["pitsnap"]);

                var pho = serializer.ConvertToType<UPhoneme>(dictionary["pho"]);

                result.PitchBend.SnapFirst = Convert.ToBoolean(dictionary["pitsnap"]);
                var pit = dictionary["pit"] as ArrayList;
                var pitshape = dictionary["pitshape"] as ArrayList;
                double x = 0, y = 0;
                result.PitchBend.Points.Clear();
                for (int i = 0; i < pit.Count; i++) {
                    if (i % 2 == 0)
                        x = Convert.ToDouble(pit[i]);
                    else {
                        y = Convert.ToDouble(pit[i]);
                        result.PitchBend.AddPoint(new PitchPoint(x, y,
                            (PitchPointShape)Enum.Parse(typeof(PitchPointShape), (string)pitshape[i / 2])));
                    }
                }

                if (dictionary.ContainsKey("vbr")) {
                    var vbr = dictionary["vbr"] as ArrayList;
                    result.Vibrato.Length = Convert.ToDouble(vbr[0]);
                    result.Vibrato.Period = Convert.ToDouble(vbr[1]);
                    result.Vibrato.Depth = Convert.ToDouble(vbr[2]);
                    result.Vibrato.In = Convert.ToDouble(vbr[3]);
                    result.Vibrato.Out = Convert.ToDouble(vbr[4]);
                    result.Vibrato.Shift = Convert.ToDouble(vbr[5]);
                    result.Vibrato.Drift = Convert.ToDouble(vbr[6]);
                }

                var exp = dictionary["exp"] as Dictionary<string, object>;
                foreach (var pair in exp)
                    result.Expressions[pair.Key].Data = pair.Value;

                return result;
            }

            public override IDictionary<string, object> Serialize(object obj, JavaScriptSerializer serializer) {
                Dictionary<string, object> result = new Dictionary<string, object>();
                var _obj = obj as UNote;
                if (_obj == null) return result;

                result.Add("y", _obj.Lyric);
                result.Add("n", _obj.NoteNum);
                result.Add("pos", _obj.PosTick);
                result.Add("dur", _obj.DurTick);
                result.Add("pho", _obj.Phoneme);

                var pit = new List<double>();
                var pitshape = new List<string>();
                foreach (var p in _obj.PitchBend.Points) {
                    pit.Add(p.X);
                    pit.Add(p.Y);
                    pitshape.Add(p.Shape.ToString());
                }

                result.Add("pitsnap", _obj.PitchBend.SnapFirst);
                result.Add("pit", pit);
                result.Add("pitshape", pitshape);

                if (_obj.Vibrato.Length > 0 && _obj.Vibrato.Depth > 0) {
                    var vbr = new List<double> {
                        _obj.Vibrato.Length,
                        _obj.Vibrato.Period,
                        _obj.Vibrato.Depth,
                        _obj.Vibrato.In,
                        _obj.Vibrato.Out,
                        _obj.Vibrato.Shift,
                        _obj.Vibrato.Drift
                    };
                    result.Add("vbr", vbr);
                }

                var exp = new Dictionary<string, int>();
                foreach (var pair in _obj.Expressions) {
                    if (pair.Value is IntExpression) {
                        exp.Add(pair.Key, (int)pair.Value.Data);
                    }
                }

                result.Add("exp", exp);

                return result;
            }
        }

        class UPhonemeConverter : JavaScriptConverter {
            public override IEnumerable<Type> SupportedTypes {
                get { return new List<Type>(new[] {typeof(UPhoneme)}); }
            }

            public override object Deserialize(IDictionary<string, object> dictionary, Type type,
                JavaScriptSerializer serializer) {
                UPhoneme result = new UPhoneme {
                    PhonemeString = dictionary["pho"] as string,
                    AutoEnvelope = Convert.ToBoolean(dictionary["autoenv"]),
                    AutoRemapped = Convert.ToBoolean(dictionary["remap"])
                };

                if (!result.AutoEnvelope) {
                    var env = dictionary["env"] as ArrayList;
                    for (int i = 0; i < 10; i++) {
                        if (i % 2 == 0)
                            result.Envelope.Points[i / 2].X = Convert.ToDouble(env[i]);
                        else
                            result.Envelope.Points[i / 2].Y = Convert.ToDouble(env[i]);
                    }
                }

                return result;
            }

            public override IDictionary<string, object> Serialize(object obj, JavaScriptSerializer serializer) {
                Dictionary<string, object> result = new Dictionary<string, object>();
                var _obj = obj as UPhoneme;
                if (_obj == null) return result;

                result.Add("pho", _obj.PhonemeString);
                result.Add("autoenv", _obj.AutoEnvelope);
                result.Add("remap", _obj.AutoRemapped);

                if (!_obj.AutoEnvelope) {
                    var env = new List<double>();
                    foreach (var p in _obj.Envelope.Points) {
                        env.Add(p.X);
                        env.Add(p.Y);
                    }

                    result.Add("env", env);
                }

                return result;
            }
        }

        class UPartConvertor : JavaScriptConverter {
            public override IEnumerable<Type> SupportedTypes {
                get { return new List<Type>(new[] {typeof(UPart), typeof(UVoicePart), typeof(UWavePart)}); }
            }

            public override object Deserialize(IDictionary<string, object> dictionary, Type type,
                JavaScriptSerializer serializer) {
                UPart result = null;
                if (dictionary.ContainsKey("notes")) {
                    result = new UVoicePart();
                    var _result = result as UVoicePart;

                    var notes = dictionary["notes"] as ArrayList;
                    foreach (var note in notes) {
                        _result.Notes.Add(serializer.ConvertToType<UNote>(note));
                    }
                } else if (dictionary.ContainsKey("path"))
                    result = Wave.CreatePart(dictionary["path"] as string);

                if (result != null) {
                    result.Name = dictionary["name"] as string;
                    result.Comment = dictionary["comment"] as string;
                    result.TrackNo = Convert.ToInt32(dictionary["track_no"]);
                    result.PosTick = Convert.ToInt32(dictionary["pos"]);
                    result.DurTick = Convert.ToInt32(dictionary["dur"]);
                }

                return result;
            }

            public override IDictionary<string, object> Serialize(object obj, JavaScriptSerializer serializer) {
                Dictionary<string, object> result = new Dictionary<string, object>();
                var _part = obj as UPart;
                if (_part == null) return result;

                result.Add("name", _part.Name);
                result.Add("comment", _part.Comment);
                result.Add("track_no", _part.TrackNo);
                result.Add("pos", _part.PosTick);
                result.Add("dur", _part.DurTick);

                if (obj is UWavePart) {
                    var _obj = obj as UWavePart;
                    result.Add("path", _obj.FilePath);
                } else if (obj is UVoicePart) {
                    var _obj = obj as UVoicePart;
                    result.Add("notes", _obj.Notes);
                }

                return result;
            }
        }

        class UProjectConvertor : JavaScriptConverter {
            public override IEnumerable<Type> SupportedTypes {
                get { return new List<Type>(new[] {typeof(UProject)}); }
            }

            public override object Deserialize(IDictionary<string, object> dictionary, Type type,
                JavaScriptSerializer serializer) {
                UProject result = new UProject();
                string ustxVersion = dictionary["ustxversion"] as string;
                Project = result;
                result.Name = dictionary["name"] as string;
                result.Comment = dictionary["comment"] as string;
                result.OutputDir = dictionary["output"] as string;
                result.CacheDir = dictionary["cache"] as string;
                result.BPM = Convert.ToDouble(dictionary["bpm"]);
                result.BeatPerBar = Convert.ToInt32(dictionary["bpbar"]);
                result.BeatUnit = Convert.ToInt32(dictionary["bunit"]);
                result.Resolution = Convert.ToInt32(dictionary["res"]);

                foreach (var pair in (Dictionary<string, object>)(dictionary["exptable"])) {
                    var exp = serializer.ConvertToType(pair.Value, typeof(IntExpression)) as IntExpression;
                    var _exp = new IntExpression(null, pair.Key, exp.Abbr) {
                        Data = exp.Data,
                        Min = exp.Min,
                        Max = exp.Max
                    };
                    result.ExpressionTable.Add(pair.Key, _exp);
                }

                foreach (var track in dictionary["tracks"] as ArrayList) {
                    var _track = serializer.ConvertToType(track, typeof(UTrack)) as UTrack;
                    result.Tracks.Add(_track);
                }

                foreach (var part in dictionary["parts"] as ArrayList)
                    result.Parts.Add(serializer.ConvertToType(part, typeof(UPart)) as UPart);

                Project = null;
                return result;
            }

            public override IDictionary<string, object> Serialize(object obj, JavaScriptSerializer serializer) {
                Dictionary<string, object> result = new Dictionary<string, object>();

                var _obj = obj as UProject;
                if (_obj != null) {
                    result.Add("ustxversion", ThisUstxVersion);
                    result.Add("name", _obj.Name);
                    result.Add("comment", _obj.Comment);
                    result.Add("output", _obj.OutputDir);
                    result.Add("cache", _obj.CacheDir);
                    result.Add("bpm", _obj.BPM);
                    result.Add("bpbar", _obj.BeatPerBar);
                    result.Add("bunit", _obj.BeatUnit);
                    result.Add("res", _obj.Resolution);
                    result.Add("tracks", _obj.Tracks.ToArray());
                    result.Add("parts", _obj.Parts);
                    result.Add("exptable", _obj.ExpressionTable);
                }

                return result;
            }
        }

        class MiscConvertor : JavaScriptConverter {
            public override IEnumerable<Type> SupportedTypes {
                get {
                    return new List<Type>(new[] {
                        typeof(IntExpression),
                        typeof(UTrack),
                        typeof(USinger)
                    });
                }
            }

            public override object Deserialize(IDictionary<string, object> dictionary, Type type,
                JavaScriptSerializer serializer) {
                if (type == typeof(IntExpression)) {
                    IntExpression result = new IntExpression(null, "", dictionary["abbr"] as string) {
                        Data = dictionary["data"],
                        Min = Convert.ToInt32(dictionary["min"]),
                        Max = Convert.ToInt32(dictionary["max"])
                    };
                    return result;
                }

                if (type == typeof(UTrack)) {
                    UTrack result = new UTrack {
                        Name = dictionary["name"] as string,
                        Comment = dictionary["comment"] as string,
                        TrackNo = Convert.ToInt32(dictionary["track_no"]),
                        Singer = serializer.ConvertToType(dictionary["singer"], typeof(USinger)) as USinger
                    };
                    return result;
                }

                if (type == typeof(USinger)) {
                    USinger result = new USinger {
                        Name = dictionary["name"] as string,
                        Path = dictionary["path"] as string
                    };
                    return result;
                }

                return null;
            }

            public override IDictionary<string, object> Serialize(object obj, JavaScriptSerializer serializer) {
                Dictionary<string, object> result = new Dictionary<string, object>();

                switch (obj) {
                    case UTrack track: {
                        result.Add("track_no", track.TrackNo);
                        result.Add("name", track.Name);
                        result.Add("comment", track.Comment);
                        result.Add("singer", track.Singer ?? new USinger {Name = "", Path = ""});
                        break;
                    }
                    case USinger singer: {
                        result.Add("name", singer.Name);
                        result.Add("path", singer.Path);

                        break;
                    }
                    case IntExpression expression: {
                        result.Add("abbr", expression.Abbr);
                        result.Add("type", expression.Type);
                        result.Add("min", expression.Min);
                        result.Add("max", expression.Max);
                        result.Add("data", expression.Data);

                        break;
                    }
                }

                return result;
            }
        }
    }
}
