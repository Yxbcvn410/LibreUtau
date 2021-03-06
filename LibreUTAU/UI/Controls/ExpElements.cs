﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LibreUtau.Core.Commands;
using LibreUtau.Core.USTx;
using LibreUtau.UI.Models;

namespace LibreUtau.UI.Controls {
    enum ExpDisMode { Hidden, Visible, Shadow }

    class ExpElement : FrameworkElement {
        protected UVoicePart _part;

        protected double _scaleX;

        protected bool _updated;

        protected double _visualHeight;

        ExpDisMode displayMode;

        public string Key;
        protected TranslateTransform tTrans;
        protected DrawingVisual visual;

        public ExpElement() {
            tTrans = new TranslateTransform();
            this.RenderTransform = tTrans;
            visual = new DrawingVisual();
            MarkUpdate();
            this.AddVisualChild(visual);
        }

        protected override int VisualChildrenCount { get { return 1; } }

        public virtual UVoicePart Part {
            set {
                _part = value;
                MarkUpdate();
            }
            get { return _part; }
        }

        public double VisualHeight {
            set {
                if (_visualHeight != value) {
                    _visualHeight = value;
                    MarkUpdate();
                }
            }
            get { return _visualHeight; }
        }

        public double ScaleX {
            set {
                if (_scaleX != value) {
                    _scaleX = value;
                    MarkUpdate();
                }
            }
            get { return _scaleX; }
        }

        public double X {
            set {
                if (tTrans.X != Math.Round(value)) { tTrans.X = Math.Round(value); }
            }
            get { return tTrans.X; }
        }

        public ExpDisMode DisplayMode {
            set {
                if (displayMode != value) {
                    displayMode = value;
                    this.Opacity = displayMode == ExpDisMode.Shadow ? 0.3 : 1;
                    this.Visibility = displayMode == ExpDisMode.Hidden ? Visibility.Hidden : Visibility.Visible;
                    if (this.Parent is Canvas) {
                        if (value == ExpDisMode.Visible) Panel.SetZIndex(this, UIConstants.ExpressionVisibleZIndex);
                        else if (value == ExpDisMode.Shadow) Panel.SetZIndex(this, UIConstants.ExpressionShadowZIndex);
                        else Panel.SetZIndex(this, UIConstants.ExpressionHiddenZIndex);
                    }
                }
            }
            get { return displayMode; }
        }

        protected override Visual GetVisualChild(int index) { return visual; }
        public void MarkUpdate() { _updated = true; }

        public virtual void RedrawIfUpdated() { }
    }

    class FloatExpElement : ExpElement {
        readonly Pen pen2;

        readonly Pen pen3;
        public MidiViewModel midiVM;

        public FloatExpElement() {
            pen3 = new Pen(ThemeManager.NoteFillBrushes[0], 3);
            pen2 = new Pen(ThemeManager.NoteFillBrushes[0], 2);
            pen3.Freeze();
            pen2.Freeze();
        }

        public override void RedrawIfUpdated() {
            if (!_updated) return;
            DrawingContext cxt = visual.RenderOpen();
            if (Part != null) {
                foreach (UNote note in Part.Notes) {
                    if (!midiVM.NoteIsInView(note)) continue;
                    if (note.Expressions.ContainsKey(Key)) {
                        var _exp = note.Expressions[Key] as IntExpression;
                        var _expTemplate = CommandDispatcher.Inst.Project.ExpressionTable[Key] as IntExpression;
                        double x1 = Math.Round(ScaleX * note.PosTick);
                        double x2 = Math.Round(ScaleX * note.EndTick);
                        double valueHeight = Math.Round(VisualHeight - VisualHeight *
                            ((int)_exp.Data - _expTemplate.Min) /
                            (_expTemplate.Max - _expTemplate.Min));
                        double zeroHeight = Math.Round(VisualHeight - VisualHeight * (0f - _expTemplate.Min) /
                            (_expTemplate.Max - _expTemplate.Min));
                        cxt.DrawLine(pen3, new Point(x1 + 0.5, zeroHeight + 0.5), new Point(x1 + 0.5, valueHeight + 3));
                        cxt.DrawEllipse(Brushes.White, pen2, new Point(x1 + 0.5, valueHeight), 2.5, 2.5);
                        cxt.DrawLine(pen2, new Point(x1 + 3, valueHeight),
                            new Point(Math.Max(x1 + 3, x2 - 3), valueHeight));
                    }
                }
            } else {
                cxt.DrawRectangle(Brushes.Transparent, null, new Rect(new Point(0, 0), new Point(100, 100)));
            }

            cxt.Close();
            _updated = false;
        }
    }
}
