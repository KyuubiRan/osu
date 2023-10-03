// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Threading;
using osu.Framework.Utils;
using osu.Game.Graphics;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Play.HUD
{
    public partial class ArgonHealthDisplay : HealthDisplay, ISerialisableDrawable
    {
        public bool UsesFixedAnchor { get; set; }

        private const float curve_start = 280;
        private const float curve_end = 310;
        private const float curve_smoothness = 10;

        private const float bar_length = 350;
        private const float bar_verticality = 32.5f;

        private BarPath healthBar = null!;
        private BarPath missBar = null!;
        private BackgroundPath background = null!;

        private SliderPath barPath = null!;

        private static readonly Colour4 main_bar_colour = Colour4.White;
        private static readonly Colour4 main_bar_glow_colour = Color4Extensions.FromHex("#7ED7FD").Opacity(0.5f);

        private ScheduledDelegate? resetMissBarDelegate;

        private readonly List<Vector2> missBarVertices = new List<Vector2>();
        private readonly List<Vector2> healthBarVertices = new List<Vector2>();

        private double missBarValue = 1;

        public double MissBarValue
        {
            get => missBarValue;
            set
            {
                if (missBarValue == value)
                    return;

                missBarValue = value;
                updatePathVertices();
            }
        }

        private double healthBarValue = 1;

        public double HealthBarValue
        {
            get => healthBarValue;
            set
            {
                if (healthBarValue == value)
                    return;

                healthBarValue = value;
                updatePathVertices();
            }
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AutoSizeAxes = Axes.Both;

            InternalChild = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(4f, 0f),
                Children = new Drawable[]
                {
                    new Circle
                    {
                        Margin = new MarginPadding { Top = 8.5f, Left = -2 },
                        Size = new Vector2(50f, 3f),
                    },
                    new Container
                    {
                        AutoSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            background = new BackgroundPath
                            {
                                PathRadius = 10f,
                            },
                            missBar = new BarPath
                            {
                                BarColour = Color4.White,
                                GlowColour = OsuColour.Gray(0.5f),
                                Blending = BlendingParameters.Additive,
                                Colour = ColourInfo.GradientHorizontal(Color4.White.Opacity(0.8f), Color4.White),
                                PathRadius = 40f,
                                // Kinda hacky, but results in correct positioning with increased path radius.
                                Margin = new MarginPadding(-30f),
                                GlowPortion = 0.9f,
                            },
                            healthBar = new BarPath
                            {
                                AutoSizeAxes = Axes.None,
                                RelativeSizeAxes = Axes.Both,
                                Blending = BlendingParameters.Additive,
                                BarColour = main_bar_colour,
                                GlowColour = main_bar_glow_colour,
                                PathRadius = 10f,
                                GlowPortion = 0.6f,
                            },
                        }
                    }
                },
            };

            updatePath();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.BindValueChanged(v =>
            {
                if (v.NewValue >= MissBarValue)
                    finishMissBarUsage();

                this.TransformTo(nameof(HealthBarValue), v.NewValue, 300, Easing.OutQuint);
                if (resetMissBarDelegate == null)
                    this.TransformTo(nameof(MissBarValue), v.NewValue, 300, Easing.OutQuint);
            }, true);
        }

        protected override void Update()
        {
            base.Update();

            healthBar.Alpha = (float)Interpolation.DampContinuously(healthBar.Alpha, Current.Value > 0 ? 1 : 0, 40, Time.Elapsed);
            missBar.Alpha = (float)Interpolation.DampContinuously(missBar.Alpha, MissBarValue > 0 ? 1 : 0, 40, Time.Elapsed);
        }

        protected override void Flash(JudgementResult result)
        {
            base.Flash(result);

            healthBar.TransformTo(nameof(BarPath.GlowColour), main_bar_glow_colour.Opacity(0.8f))
                     .TransformTo(nameof(BarPath.GlowColour), main_bar_glow_colour, 300, Easing.OutQuint);

            if (resetMissBarDelegate == null)
            {
                missBar.TransformTo(nameof(BarPath.BarColour), Colour4.White, 100, Easing.OutQuint)
                       .Then()
                       .TransformTo(nameof(BarPath.BarColour), main_bar_colour, 800, Easing.OutQuint);

                missBar.TransformTo(nameof(BarPath.GlowColour), Colour4.White)
                       .TransformTo(nameof(BarPath.GlowColour), main_bar_glow_colour, 800, Easing.OutQuint);
            }
        }

        protected override void Miss(JudgementResult result)
        {
            base.Miss(result);

            if (resetMissBarDelegate != null)
            {
                resetMissBarDelegate.Cancel();
                resetMissBarDelegate = null;
            }
            else
                this.TransformTo(nameof(MissBarValue), HealthBarValue);

            this.Delay(500).Schedule(() =>
            {
                this.TransformTo(nameof(MissBarValue), Current.Value, 300, Easing.OutQuint);
                finishMissBarUsage();
            }, out resetMissBarDelegate);

            missBar.TransformTo(nameof(BarPath.BarColour), new Colour4(255, 147, 147, 255), 100, Easing.OutQuint).Then()
                   .TransformTo(nameof(BarPath.BarColour), new Colour4(255, 93, 93, 255), 800, Easing.OutQuint);

            missBar.TransformTo(nameof(BarPath.GlowColour), new Colour4(253, 0, 0, 255).Lighten(0.2f))
                   .TransformTo(nameof(BarPath.GlowColour), new Colour4(253, 0, 0, 255), 800, Easing.OutQuint);
        }

        private void finishMissBarUsage()
        {
            if (Current.Value > 0)
            {
                missBar.TransformTo(nameof(BarPath.BarColour), main_bar_colour, 300, Easing.In);
                missBar.TransformTo(nameof(BarPath.GlowColour), main_bar_glow_colour, 300, Easing.In);
            }

            resetMissBarDelegate?.Cancel();
            resetMissBarDelegate = null;
        }

        private void updatePath()
        {
            Vector2 diagonalDir = (new Vector2(curve_end, bar_verticality) - new Vector2(curve_start, 0)).Normalized();

            barPath = new SliderPath(new[]
            {
                new PathControlPoint(new Vector2(0, 0), PathType.Linear),
                new PathControlPoint(new Vector2(curve_start - curve_smoothness, 0), PathType.Bezier),
                new PathControlPoint(new Vector2(curve_start, 0)),
                new PathControlPoint(new Vector2(curve_start, 0) + diagonalDir * curve_smoothness, PathType.Linear),
                new PathControlPoint(new Vector2(curve_end, bar_verticality) - diagonalDir * curve_smoothness, PathType.Bezier),
                new PathControlPoint(new Vector2(curve_end, bar_verticality)),
                new PathControlPoint(new Vector2(curve_end + curve_smoothness, bar_verticality), PathType.Linear),
                new PathControlPoint(new Vector2(bar_length, bar_verticality)),
            });

            List<Vector2> vertices = new List<Vector2>();
            barPath.GetPathToProgress(vertices, 0.0, 1.0);

            background.Vertices = vertices;
            healthBar.Vertices = vertices;
            missBar.Vertices = vertices;

            updatePathVertices();
        }

        private void updatePathVertices()
        {
            barPath.GetPathToProgress(healthBarVertices, 0.0, healthBarValue);
            barPath.GetPathToProgress(missBarVertices, healthBarValue, Math.Max(missBarValue, healthBarValue));

            if (healthBarVertices.Count == 0)
                healthBarVertices.Add(Vector2.Zero);

            if (missBarVertices.Count == 0)
                missBarVertices.Add(Vector2.Zero);

            missBar.Vertices = missBarVertices.Select(v => v - missBarVertices[0]).ToList();
            missBar.Position = missBarVertices[0];

            healthBar.Vertices = healthBarVertices.Select(v => v - healthBarVertices[0]).ToList();
            healthBar.Position = healthBarVertices[0];
        }

        private partial class BackgroundPath : SmoothPath
        {
            protected override Color4 ColourAt(float position)
            {
                if (position <= 0.128f)
                    return Color4.White.Opacity(0.8f);

                return Interpolation.ValueAt(position,
                    Color4.White.Opacity(0.8f),
                    Color4.Black.Opacity(0.2f),
                    -0.5f, 1f, Easing.OutQuint);
            }
        }

        private partial class BarPath : SmoothPath
        {
            private Colour4 barColour;

            public Colour4 BarColour
            {
                get => barColour;
                set
                {
                    if (barColour == value)
                        return;

                    barColour = value;
                    InvalidateTexture();
                }
            }

            private Colour4 glowColour;

            public Colour4 GlowColour
            {
                get => glowColour;
                set
                {
                    if (glowColour == value)
                        return;

                    glowColour = value;
                    InvalidateTexture();
                }
            }

            public float GlowPortion { get; init; }

            protected override Color4 ColourAt(float position)
            {
                if (position >= GlowPortion)
                    return BarColour;

                return Interpolation.ValueAt(position, Colour4.Black.Opacity(0.0f), GlowColour, 0.0, GlowPortion, Easing.InQuint);
            }
        }
    }
}
