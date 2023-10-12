// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections.Input
{
    public partial class KeyBindingConflictPopover : OsuPopover
    {
        public Bindable<KeyBindingConflictInfo> ConflictInfo { get; } = new Bindable<KeyBindingConflictInfo>();

        public Action? BindingConflictResolved { get; init; }

        private ConflictingKeyBindingPreview newPreview = null!;
        private ConflictingKeyBindingPreview existingPreview = null!;
        private HoverableRoundedButton keepExistingButton = null!;
        private HoverableRoundedButton applyNewButton = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load() => recreateDisplay();

        private void recreateDisplay()
        {
            Child = new FillFlowContainer
            {
                Width = 250,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(10),
                Children = new Drawable[]
                {
                    new OsuTextFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Text = "The binding you've selected conflicts with another existing binding.",
                        Margin = new MarginPadding { Bottom = 10 }
                    },
                    existingPreview = new ConflictingKeyBindingPreview(ConflictInfo.Value.Existing.Action, ConflictInfo.Value.Existing.CombinationWhenChosen, ConflictInfo.Value.Existing.CombinationWhenNotChosen),
                    newPreview = new ConflictingKeyBindingPreview(ConflictInfo.Value.New.Action, ConflictInfo.Value.New.CombinationWhenChosen, ConflictInfo.Value.New.CombinationWhenNotChosen),
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Margin = new MarginPadding { Top = 10 },
                        Children = new[]
                        {
                            keepExistingButton = new HoverableRoundedButton
                            {
                                Text = "Keep existing",
                                RelativeSizeAxes = Axes.X,
                                Width = 0.48f,
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Action = Hide
                            },
                            applyNewButton = new HoverableRoundedButton
                            {
                                Text = "Apply new",
                                RelativeSizeAxes = Axes.X,
                                Width = 0.48f,
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                Action = applyNew
                            }
                        }
                    }
                }
            };
        }

        private void applyNew()
        {
            // only "apply new" needs to cause actual realm changes, since the flow in `KeyBindingsSubsection` does not actually make db changes
            // if it detects a binding conflict.
            // the temporary visual changes will be reverted by calling `Hide()` / `BindingConflictResolved`.
            realm.Write(r =>
            {
                var existingBinding = r.Find<RealmKeyBinding>(ConflictInfo.Value.Existing.ID);
                existingBinding!.KeyCombinationString = ConflictInfo.Value.Existing.CombinationWhenNotChosen.ToString();

                var newBinding = r.Find<RealmKeyBinding>(ConflictInfo.Value.New.ID);
                newBinding!.KeyCombinationString = ConflictInfo.Value.Existing.CombinationWhenChosen.ToString();
            });

            Hide();
        }

        protected override void PopOut()
        {
            base.PopOut();

            // workaround for `VisibilityContainer.PopOut()` being called in `LoadAsyncComplete()`
            if (IsLoaded)
                BindingConflictResolved?.Invoke();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            keepExistingButton.IsHoveredBindable.BindValueChanged(_ => updatePreviews());
            applyNewButton.IsHoveredBindable.BindValueChanged(_ => updatePreviews());
            updatePreviews();
        }

        private void updatePreviews()
        {
            if (!keepExistingButton.IsHovered && !applyNewButton.IsHovered)
            {
                existingPreview.IsChosen.Value = newPreview.IsChosen.Value = null;
                return;
            }

            existingPreview.IsChosen.Value = keepExistingButton.IsHovered;
            newPreview.IsChosen.Value = applyNewButton.IsHovered;
        }

        private partial class ConflictingKeyBindingPreview : CompositeDrawable
        {
            private readonly object action;
            private readonly KeyCombination combinationWhenChosen;
            private readonly KeyCombination combinationWhenNotChosen;

            private OsuSpriteText newBindingText = null!;

            public Bindable<bool?> IsChosen { get; } = new Bindable<bool?>();

            [Resolved]
            private ReadableKeyCombinationProvider keyCombinationProvider { get; set; } = null!;

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            public ConflictingKeyBindingPreview(object action, KeyCombination combinationWhenChosen, KeyCombination combinationWhenNotChosen)
            {
                this.action = action;
                this.combinationWhenChosen = combinationWhenChosen;
                this.combinationWhenNotChosen = combinationWhenNotChosen;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                InternalChild = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    CornerRadius = 5,
                    Masking = true,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background5
                        },
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                            ColumnDimensions = new[]
                            {
                                new Dimension(),
                                new Dimension(GridSizeMode.AutoSize),
                            },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Text = action.GetLocalisableDescription(),
                                        Margin = new MarginPadding(7.5f),
                                    },
                                    new Container
                                    {
                                        AutoSizeAxes = Axes.Both,
                                        CornerRadius = 5,
                                        Masking = true,
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        X = -5,
                                        Children = new[]
                                        {
                                            new Box
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Colour = colourProvider.Background6
                                            },
                                            Empty().With(d => d.Width = 80), // poor man's min-width
                                            newBindingText = new OsuSpriteText
                                            {
                                                Font = OsuFont.Numeric.With(size: 10),
                                                Margin = new MarginPadding(5),
                                                Anchor = Anchor.Centre,
                                                Origin = Anchor.Centre
                                            }
                                        }
                                    },
                                }
                            }
                        }
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                IsChosen.BindValueChanged(_ => updateState(), true);
            }

            private void updateState()
            {
                string newBinding;

                switch (IsChosen.Value)
                {
                    case true:
                        newBinding = keyCombinationProvider.GetReadableString(combinationWhenChosen);
                        newBindingText.Colour = colours.Green1;
                        break;

                    case false:
                        newBinding = keyCombinationProvider.GetReadableString(combinationWhenNotChosen);
                        newBindingText.Colour = colours.Red1;
                        break;

                    case null:
                        newBinding = keyCombinationProvider.GetReadableString(combinationWhenChosen);
                        newBindingText.Colour = Colour4.White;
                        break;
                }

                if (string.IsNullOrEmpty(newBinding))
                    newBinding = "(none)";

                newBindingText.Text = newBinding;
            }
        }

        private partial class HoverableRoundedButton : RoundedButton
        {
            public BindableBool IsHoveredBindable { get; set; } = new BindableBool();

            protected override bool OnHover(HoverEvent e)
            {
                IsHoveredBindable.Value = IsHovered;
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                IsHoveredBindable.Value = IsHovered;
                base.OnHoverLost(e);
            }
        }
    }
}
