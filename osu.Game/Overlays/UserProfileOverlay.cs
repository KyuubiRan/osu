﻿// Copyright (c) 2007-2017 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using System.Linq;
using OpenTK;
using OpenTK.Graphics;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Overlays.Profile;
using osu.Game.Users;

namespace osu.Game.Overlays
{
    public class UserProfileOverlay : WaveOverlayContainer
    {
        private ProfileSection lastSection;
        private ProfileSection[] sections;
        private GetUserRequest userReq;
        private APIAccess api;
        private ProfileHeader header;
        private SectionsContainer<ProfileSection> sectionsContainer;
        private ProfileTabControl tabs;

        public const float CONTENT_X_MARGIN = 50;
        private const float transition_length = 500;

        public UserProfileOverlay()
        {
            FirstWaveColour = OsuColour.Gray(0.4f);
            SecondWaveColour = OsuColour.Gray(0.3f);
            ThirdWaveColour = OsuColour.Gray(0.2f);
            FourthWaveColour = OsuColour.Gray(0.1f);

            RelativeSizeAxes = Axes.Both;
            RelativePositionAxes = Axes.Both;
            Width = 0.85f;
            Anchor = Anchor.TopCentre;
            Origin = Anchor.TopCentre;

            Masking = true;
            EdgeEffect = new EdgeEffectParameters
            {
                Colour = Color4.Black.Opacity(0),
                Type = EdgeEffectType.Shadow,
                Radius = 10
            };
        }

        [BackgroundDependencyLoader]
        private void load(APIAccess api)
        {
            this.api = api;
        }

        protected override void PopIn()
        {
            base.PopIn();
            FadeEdgeEffectTo(0.5f, APPEAR_DURATION, EasingTypes.In);
        }

        protected override void PopOut()
        {
            base.PopOut();
            FadeEdgeEffectTo(0, DISAPPEAR_DURATION, EasingTypes.Out);
        }

        public void ShowUser(User user, bool fetchOnline = true)
        {
            userReq?.Cancel();
            Clear();
            lastSection = null;

            sections = new ProfileSection[]
            {
                new AboutSection(),
                new RecentSection(),
                new RanksSection(),
                new MedalsSection(),
                new HistoricalSection(),
                new BeatmapsSection(),
                new KudosuSection()
            };
            tabs = new ProfileTabControl
            {
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Height = 30
            };

            Add(new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = OsuColour.Gray(0.2f)
            });

            header = new ProfileHeader(user);

            Add(sectionsContainer = new SectionsContainer<ProfileSection>
            {
                RelativeSizeAxes = Axes.Both,
                ExpandableHeader = header,
                FixedHeader = tabs,
                HeaderBackground = new Box
                {
                    Colour = OsuColour.Gray(34),
                    RelativeSizeAxes = Axes.Both
                }
            });
            sectionsContainer.SelectedSection.ValueChanged += s =>
            {
                if (lastSection != s)
                {
                    lastSection = s;
                    tabs.Current.Value = lastSection;
                }
            };

            tabs.Current.ValueChanged += s =>
            {
                if (lastSection == null)
                {
                    lastSection = sectionsContainer.Children.FirstOrDefault();
                    if (lastSection != null)
                        tabs.Current.Value = lastSection;
                    return;
                }
                if (lastSection != s)
                {
                    lastSection = s;
                    sectionsContainer.ScrollToTop(lastSection);
                }
            };

            if (fetchOnline)
            {
                userReq = new GetUserRequest(user.Id);
                userReq.Success += fillData;
                api.Queue(userReq);
            }
            else
            {
                userReq = null;
                fillData(user);
            }

            Show();
        }

        private void fillData(User user)
        {
            header.FillFullData(user);

            for (int i = 0; i < user.ProfileOrder.Length; i++)
            {
                string id = user.ProfileOrder[i];
                var sec = sections.FirstOrDefault(s => s.Identifier == id);
                if (sec != null)
                {
                    sec.Depth = -i;
                    sectionsContainer.Add(sec);
                    tabs.AddItem(sec);
                }
            }
        }

        private class ProfileTabControl : PageTabControl<ProfileSection>
        {
            private readonly Box bottom;

            public ProfileTabControl()
            {
                TabContainer.RelativeSizeAxes &= ~Axes.X;
                TabContainer.AutoSizeAxes |= Axes.X;
                TabContainer.Anchor |= Anchor.x1;
                TabContainer.Origin |= Anchor.x1;
                Add(bottom = new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 1,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    EdgeSmoothness = new Vector2(1)
                });
            }

            protected override TabItem<ProfileSection> CreateTabItem(ProfileSection value) => new ProfileTabItem(value);

            protected override Dropdown<ProfileSection> CreateDropdown() => null;

            private class ProfileTabItem : PageTabItem
            {
                public ProfileTabItem(ProfileSection value) : base(value)
                {
                    Text.Text = value.Title;
                }
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                bottom.Colour = colours.Yellow;
            }
        }
    }
}
