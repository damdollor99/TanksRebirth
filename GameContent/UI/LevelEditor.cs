﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TanksRebirth.Enums;
using TanksRebirth.GameContent.Properties;
using TanksRebirth.GameContent.Systems;
using TanksRebirth.Internals;
using TanksRebirth.Internals.Common;
using TanksRebirth.Internals.Common.Framework.Audio;
using TanksRebirth.Internals.Common.GameUI;
using TanksRebirth.Internals.Common.Utilities;
using TanksRebirth.Internals.UI;
using FontStashSharp;
using TanksRebirth.GameContent.Systems.Coordinates;
using NativeFileDialogSharp;
using TanksRebirth.GameContent.ID;
using TanksRebirth.Internals.Common.Framework.Graphics;

namespace TanksRebirth.GameContent.UI
{
    public static class LevelEditor
    {
        public static bool Active { get; private set; }
        public static OggMusic Theme = new("Level Editor Theme", "Content/Assets/mainmenu/editor", 0.7f);

        public static UITextButton TestLevel;
        public static UITextButton Perspective;

        public static UITextButton BlocksCategory;
        public static UITextButton EnemyTanksCategory;
        public static UITextButton PlayerTanksCategory;

        public static UITextButton Properties;
        public static UITextButton LoadLevel;

        public static UITextButton ReturnToEditor;

        #region ConfirmLevelContents
        // finish confirmation stuff later.
        public static Rectangle LevelContentsPanel;
        public static UITextInput MissionName;
        public static UITextInput CampaignName;
        public static UITextInput CampaignDescription;
        public static UITextInput CampaignAuthor;
        public static UITextInput CampaignVersion;
        public static UITextInput CampaignTags;
        public static UITextInput CampaignExtraLives;
        public static UITextInput CampaignLoadingStripColor;
        public static UITextInput CampaignLoadingBGColor;
        public static UITextButton SaveMenuReturn;
        public static UITextButton SaveLevelConfirm;
        public static UITextButton CampaignMajorVictory;

        public static UITextButton SwapMenu;
        #endregion
        #region Variables / Properties
        public static Category CurCategory { get; private set; }
        public static int SelectedTankTier { get; private set; }
        public static int SelectedTankTeam { get; private set; }
        public static int SelectedPlayerType { get; private set; }
        public static int SelectedBlockType { get; private set; }
        public static int BlockHeight { get; private set; }
        public static bool Editing { get; internal set; }

        private static Mission _cachedMission;
        public enum Category {
            EnemyTanks,
            Blocks,
            PlayerTanks
        }

        public static Dictionary<string, Texture2D> RenderTextures = new();

        private static float _barOffset;
        private static Vector2 _origClick;
        private static float _maxScroll;
        private static List<string> _renderNamesTanks = new();
        private static List<string> _renderNamesBlocks = new();
        private static List<string> _renderNamesPlayers = new();
        #endregion

        private static bool _initialized;

        private static Campaign _loadedCampaign;

        private static bool _viewMissionDetails = true;
        private static bool _hasMajorVictory;

        private static readonly List<UITextInput> _campaignElems = new();

        // reduce hardcode -- make a variable that tracks height.
        public static void InitializeSaveMenu()
        {
            LevelContentsPanel = new Rectangle(GameUtils.WindowWidth / 4, (int)(GameUtils.WindowHeight * 0.1f), GameUtils.WindowWidth / 2, (int)(GameUtils.WindowHeight * 0.625f));

            MissionName = new(TankGame.TextFont, Color.White, 1f, 20);

            MissionName.SetDimensions(() => new(LevelContentsPanel.X + 20.ToResolutionX(),
                LevelContentsPanel.Y + 60.ToResolutionY()),
                () => new(LevelContentsPanel.Width - 40.ToResolutionX(),
                50.ToResolutionY()));
            MissionName.DefaultString = "Name";

            SaveMenuReturn = new("Return", TankGame.TextFont, Color.White);
            SaveMenuReturn.SetDimensions(() => new Vector2(LevelContentsPanel.X + 20.ToResolutionX(),
                LevelContentsPanel.Y + LevelContentsPanel.Height - 60.ToResolutionY()),
                () => new(200.ToResolutionX(),
                50.ToResolutionY()));

            SaveMenuReturn.OnLeftClick = (l) => {
                GUICategory = UICategory.LevelEditor;
                if (MissionName.GetRealText() != string.Empty)
                    _loadedCampaign.CachedMissions[_loadedCampaign.CurrentMissionId].Name = MissionName.GetRealText();
                SetupMissionsBar(_loadedCampaign);
            };

            SaveLevelConfirm = new("Save", TankGame.TextFont, Color.White);
            SaveLevelConfirm.SetDimensions(() => new Vector2(LevelContentsPanel.X + 40.ToResolutionX() + SaveMenuReturn.Size.X,
                LevelContentsPanel.Y + LevelContentsPanel.Height - 60.ToResolutionY()),
                () => new(200.ToResolutionX(),
                50.ToResolutionY()));
            SaveLevelConfirm.Tooltip = "Save your mission to a file.\nKeep in mind: you will need to name the missions in numerical order.";
            SaveLevelConfirm.OnLeftClick = (l) => {
                // Mission.Save(LevelName.GetRealText(), )
                var res = Dialog.FileSave(_viewMissionDetails ? "mission" : "campaign", TankGame.SaveDirectory);
                if (res.Path != null && res.IsOk)
                {
                    try {
                        var name = _viewMissionDetails ? MissionName.Text : CampaignName.Text;
                        var realName = Path.HasExtension(res.Path) ? Path.GetFileNameWithoutExtension(res.Path) : Path.GetFileName(res.Path);
                        var path = res.Path.Replace(realName, string.Empty);
                        if (_loadedCampaign == null)
                            Mission.Save(name, path, false, realName);
                        else
                        {
                            _loadedCampaign.MetaData.Name = CampaignName.Text;
                            _loadedCampaign.MetaData.Description = CampaignDescription.Text;
                            _loadedCampaign.MetaData.Author = CampaignAuthor.Text;
                            _loadedCampaign.MetaData.Tags = CampaignTags.Text.Split(',');
                            _loadedCampaign.MetaData.ExtraLivesMissions = ArrayUtils.SequenceToInt32Array(CampaignExtraLives.Text);
                            _loadedCampaign.MetaData.Version = CampaignVersion.Text;
                            _loadedCampaign.MetaData.BackgroundColor = UnpackedColor.FromStringFormat(CampaignLoadingBGColor.Text);
                            _loadedCampaign.MetaData.MissionStripColor = UnpackedColor.FromStringFormat(CampaignLoadingStripColor.Text);
                            _loadedCampaign.MetaData.HasMajorVictory = _hasMajorVictory;
                            Campaign.Save(Path.Combine(path, realName), _loadedCampaign);
                        }
                    }
                    catch {
                        // guh...
                    }
                }

                // GUICategory = UICategory.LevelEditor;
            };

            float width = 300;
            float height = 50;

            SwapMenu = new("Campaign Details", TankGame.TextFont, Color.White);
            SwapMenu.SetDimensions(() => new Vector2(LevelContentsPanel.X + LevelContentsPanel.Width - width.ToResolutionX() - 20.ToResolutionX(),
                LevelContentsPanel.Y + LevelContentsPanel.Height - height.ToResolutionY() - 10.ToResolutionY()),
                () => new(width.ToResolutionX(),
                height.ToResolutionY()));

            SwapMenu.OnLeftClick = (l) => {
                _viewMissionDetails = !_viewMissionDetails;
            };

            CampaignName = new(TankGame.TextFont, Color.White, 1f, 30);
            CampaignName.SetDimensions(() => new Vector2(LevelContentsPanel.X + 20.ToResolutionX(),
                LevelContentsPanel.Y + 60.ToResolutionY()),
                () => new(LevelContentsPanel.Width - 40.ToResolutionX(),
                50.ToResolutionY()));
            CampaignName.DefaultString = "Campaign Name";
            CampaignName.Tooltip = "The campaign this mission belongs to.";

            CampaignDescription = new(TankGame.TextFont, Color.White, 1f, 100);
            CampaignDescription.SetDimensions(() => new Vector2(LevelContentsPanel.X + 20.ToResolutionX(), LevelContentsPanel.Y + 120.ToResolutionY()), () => new(LevelContentsPanel.Width - 40.ToResolutionX(), 50.ToResolutionY()));
            CampaignDescription.DefaultString = "Description";

            CampaignAuthor = new(TankGame.TextFont, Color.White, 1f, 25);
            CampaignAuthor.SetDimensions(() => new Vector2(LevelContentsPanel.X + 20.ToResolutionX(), LevelContentsPanel.Y + 180.ToResolutionY()), () => new(LevelContentsPanel.Width - 40.ToResolutionX(), 50.ToResolutionY()));
            CampaignAuthor.DefaultString = "Author";

            CampaignTags = new(TankGame.TextFont, Color.White, 1f, 35);
            CampaignTags.SetDimensions(() => new Vector2(LevelContentsPanel.X + 20.ToResolutionX(), LevelContentsPanel.Y + 240.ToResolutionY()), () => new(LevelContentsPanel.Width - 40.ToResolutionX(), 50.ToResolutionY()));
            CampaignTags.DefaultString = "Tags (split with ',')";

            CampaignExtraLives = new(TankGame.TextFont, Color.White, 1f, 100);
            CampaignExtraLives.SetDimensions(() => new Vector2(LevelContentsPanel.X + 20.ToResolutionX(), LevelContentsPanel.Y + 300.ToResolutionY()), () => new(LevelContentsPanel.Width - 40.ToResolutionX(), 50.ToResolutionY()));
            CampaignExtraLives.DefaultString = "Extra Life Missions (split with ',')";

            CampaignVersion = new(TankGame.TextFont, Color.White, 1f, 10);
            CampaignVersion.SetDimensions(() => new Vector2(LevelContentsPanel.X + 20.ToResolutionX(), LevelContentsPanel.Y + 360.ToResolutionY()), () => new(LevelContentsPanel.Width - 40.ToResolutionX(), 50.ToResolutionY()));
            CampaignVersion.DefaultString = "Level Version";

            CampaignLoadingBGColor = new(TankGame.TextFont, Color.White, 1f, 11);
            CampaignLoadingBGColor.SetDimensions(() => new Vector2(LevelContentsPanel.X + 20.ToResolutionX(), LevelContentsPanel.Y + 420.ToResolutionY()), () => new(LevelContentsPanel.Width - 40.ToResolutionX(), 50.ToResolutionY()));
            CampaignLoadingBGColor.DefaultString = "Mission Loading: BG Color";

            CampaignLoadingStripColor = new(TankGame.TextFont, Color.White, 1f, 11);
            CampaignLoadingStripColor.SetDimensions(() => new Vector2(LevelContentsPanel.X + 20.ToResolutionX(), LevelContentsPanel.Y + 480.ToResolutionY()), () => new(LevelContentsPanel.Width - 40.ToResolutionX(), 50.ToResolutionY()));
            CampaignLoadingStripColor.DefaultString = "Mission Loading: Strip Color";

            CampaignMajorVictory = new("", TankGame.TextFont, Color.White, () => Vector2.One.ToResolution());
            CampaignMajorVictory.SetDimensions(() => new Vector2(LevelContentsPanel.X + 20.ToResolutionX(), LevelContentsPanel.Y + 540.ToResolutionY()), () => new(LevelContentsPanel.Width - 40.ToResolutionX(), 50.ToResolutionY()));
            CampaignMajorVictory.OnLeftClick = (a) => _hasMajorVictory = !_hasMajorVictory;
            SetSaveMenuVisibility(false);

            _campaignElems.Add(MissionName);
            _campaignElems.Add(CampaignName);
            _campaignElems.Add(CampaignVersion);
            _campaignElems.Add(CampaignLoadingBGColor);
            _campaignElems.Add(CampaignLoadingStripColor);
            _campaignElems.Add(CampaignTags);
            _campaignElems.Add(CampaignAuthor);
            _campaignElems.Add(CampaignExtraLives);
            _campaignElems.Add(CampaignDescription);
        }
        public enum UICategory {
            LevelEditor,
            SavingThings,
        }

        private static bool _sdbui;
        public static bool ShouldDrawBarUI {
            get => _sdbui;
            set {
                if (GUICategory == UICategory.SavingThings)
                    SetSaveMenuVisibility(value);
                SetBarUIVisibility(value);
                _sdbui = value;
            }
        }

        private static UICategory _category;
        public static UICategory GUICategory {
            get => _category;
            set {
                _category = value;

                if (_category == UICategory.SavingThings)
                {
                    SetSaveMenuVisibility(true);
                    SetLevelEditorVisibility(false);
                }
                else
                {
                    SetSaveMenuVisibility(false);
                    SetLevelEditorVisibility(true);
                }
            }
        }

        private static List<UITextButton> _missionButtons = new();
        private static Rectangle _missionTab = new(0, 150, 350, 500);
        private static Rectangle _missionButtonScissor;
        private static float _missionsOffset;
        private static float _missionsMaxOff;

        private static bool _saveMenuOpen;
        private static void SetBarUIVisibility(bool visible)
        {
            PlayerTanksCategory.IsVisible =
                EnemyTanksCategory.IsVisible =
                BlocksCategory.IsVisible =
                Perspective.IsVisible =
                Properties.IsVisible = 
                LoadLevel.IsVisible = 
                TestLevel.IsVisible = visible;
            _missionButtons.ForEach(x => x.IsVisible = visible);
        }
        private static void SetSaveMenuVisibility(bool visible)
        {
            _saveMenuOpen = visible;
            MissionName.IsVisible = visible && _viewMissionDetails;
            SaveLevelConfirm.IsVisible = visible;
            SaveMenuReturn.IsVisible = visible;
            SwapMenu.IsVisible = visible;
            SaveMenuReturn.IsVisible = visible;

            CampaignName.IsVisible = visible && !_viewMissionDetails;
            CampaignDescription.IsVisible = visible && !_viewMissionDetails;
            CampaignAuthor.IsVisible = visible && !_viewMissionDetails;
            CampaignTags.IsVisible = visible && !_viewMissionDetails;
            CampaignVersion.IsVisible = visible && !_viewMissionDetails;
            CampaignExtraLives.IsVisible = visible && !_viewMissionDetails;
            CampaignLoadingBGColor.IsVisible = visible && !_viewMissionDetails;
            CampaignLoadingStripColor.IsVisible = visible && !_viewMissionDetails;
            CampaignMajorVictory.IsVisible = visible && !_viewMissionDetails;
        }
        private static void SetLevelEditorVisibility(bool visible)
        {
            EnemyTanksCategory.IsVisible = visible;
            BlocksCategory.IsVisible = visible;
            TestLevel.IsVisible = visible;
            Perspective.IsVisible = visible;
            PlayerTanksCategory.IsVisible = visible;
            Properties.IsVisible = visible;
            LoadLevel.IsVisible = visible;
        }
        public static void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;
            #region Enumerable Init
            for (int i = 1; i < TeamID.Collection.Count; i++)
            {
                var team = TeamID.Collection.GetKey(i);

                var colorToAdd = (Color)typeof(Color).GetProperty(team).GetValue(null);
                TeamColors.Add(colorToAdd);
            }
            foreach (var file in Directory.GetFiles(Path.Combine("Content", "Assets", "textures", "ui", "leveledit")))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var assetName = file;
                RenderTextures.Add(fileName, GameResources.GetGameResource<Texture2D>(assetName, false, false));
            }
            var names = TankID.Collection.Keys;
            for (int i = 0; i < names.Length; i++) {
                var nTL = names[i];

                if (RenderTextures.ContainsKey(nTL))
                    _renderNamesTanks.Add(nTL);
            }
            names = BlockID.Collection.Keys;
            for (int i = 0; i < names.Length; i++) {
                var nTL = names[i];

                if (RenderTextures.ContainsKey(nTL))
                    _renderNamesBlocks.Add(nTL);
            }
            names = PlayerID.Collection.Keys;
            for (int i = 0; i < names.Length; i++) {
                var nTL = names[i];

                if (RenderTextures.ContainsKey(nTL))
                    _renderNamesPlayers.Add(nTL);
            }
            #endregion

            TestLevel = new("Test Level", TankGame.TextFont, Color.White);
            TestLevel.SetDimensions(() => new(GameUtils.WindowWidth * 0.01f, GameUtils.WindowHeight * 0.725f), () => new Vector2(200, 50).ToResolution());

            TestLevel.OnLeftClick = (l) => {
                Close();
                TankGame.OverheadView = false;

                _cachedMission = Mission.GetCurrent();
            };

            ReturnToEditor = new("Return to Editor", TankGame.TextFont, Color.White);
            ReturnToEditor.SetDimensions(() => new(GameUtils.WindowWidth * 0.01f, GameUtils.WindowHeight * 0.02f), () => new Vector2(250, 50).ToResolution());

            ReturnToEditor.OnLeftClick = (l) => {
                Open(false);
                TankGame.OverheadView = true;
                GameProperties.InMission = false;
                GameHandler.CleanupScene();
                Mission.LoadDirectly(_cachedMission);
            };

            Perspective = new("Perspective", TankGame.TextFont, Color.White);
            Perspective.SetDimensions(() => new(GameUtils.WindowWidth * 0.125f, GameUtils.WindowHeight * 0.725f), () => new Vector2(200, 50).ToResolution());
            Perspective.Tooltip = "View your mission at the perspective of the player.";
            Perspective.OnLeftClick = (l) => {
                TankGame.OverheadView = !TankGame.OverheadView;
            };

            BlocksCategory = new("Blocks", TankGame.TextFont, Color.White);
            BlocksCategory.SetDimensions(() => new(GameUtils.WindowWidth * 0.75f, GameUtils.WindowHeight * 0.725f), () => new Vector2(200, 50).ToResolution());
            BlocksCategory.OnLeftClick = (l) => {
                CurCategory = Category.Blocks;
            };

            EnemyTanksCategory = new("Enemies", TankGame.TextFont, Color.White);
            EnemyTanksCategory.SetDimensions(() => new(GameUtils.WindowWidth * 0.875f, GameUtils.WindowHeight * 0.725f), () => new Vector2(200, 50).ToResolution());
            EnemyTanksCategory.OnLeftClick = (l) => {
                CurCategory = Category.EnemyTanks;
            };
            PlayerTanksCategory = new("Players", TankGame.TextFont, Color.White);
            PlayerTanksCategory.SetDimensions(() => new(GameUtils.WindowWidth * 0.875f, GameUtils.WindowHeight * 0.65f), () => new Vector2(200, 50).ToResolution());
            PlayerTanksCategory.OnLeftClick = (l) => {
                CurCategory = Category.PlayerTanks;
            };

            Properties = new("Properties", TankGame.TextFont, Color.White);

            float width = 200;

            Properties.SetDimensions(() => new(GameUtils.WindowWidth * 0.425f - (width / 2).ToResolutionX(), 10.ToResolutionY()), () => new Vector2(width, 50).ToResolution());
            Properties.OnLeftClick = (a) => {
                if (!_saveMenuOpen)
                    GUICategory = UICategory.SavingThings;
                else
                    GUICategory = UICategory.LevelEditor;
            };

            LoadLevel = new("Load", TankGame.TextFont, Color.White);

            LoadLevel.SetDimensions(() => new(GameUtils.WindowWidth * 0.575f - (width / 2).ToResolutionX(), 10.ToResolutionY()), () => new Vector2(width, 50).ToResolution());
            LoadLevel.OnLeftClick = (a) => {
                var res = Dialog.FileOpen("mission,campaign", TankGame.SaveDirectory);
                if (res.Path != null && res.IsOk)
                {
                    try {
                        var ext = Path.GetExtension(res.Path);

                        if (ext == ".mission") {
                            //GameProperties.LoadedCampaign.LoadMission(Mission.Load(res.Path, null));
                            //GameProperties.LoadedCampaign.SetupLoadedMission(true);
                            Mission.LoadDirectly(Mission.Load(res.Path, null));
                            //_loadedCampaign = null;
                        }
                        else {
                            _loadedCampaign = Campaign.Load(res.Path);
                            _loadedCampaign.LoadMission(0);
                            _loadedCampaign.SetupLoadedMission(true);
                            MissionName.Text = _loadedCampaign.CachedMissions[0].Name;
                            SetupMissionsBar(_loadedCampaign);
                        }

                        ChatSystem.SendMessage($"Loaded '{Path.GetFileName(res.Path)}'.", Color.White);
                    } catch {
                        ChatSystem.SendMessage("Failed to load.", Color.Red);
                    }
                }
                return;
            };
            // TODO: non-windows support. i am lazy. fuck this.
            LoadLevel.Tooltip = "Will open a file dialog for\nyou to choose what mission/campaign to load.";

            InitializeSaveMenu();

            SetLevelEditorVisibility(false);

            UIElement.CunoSucks();
        }
        public static void SetupMissionsBar(Campaign campaign)
        {
            RemoveMissionButtons();

            // TODO: scissor, etc
            // offset
            // campaign metadata editing
            // go to bed ryan

            CampaignName.Text = campaign.MetaData.Name;
            CampaignDescription.Text = campaign.MetaData.Description;
            CampaignAuthor.Text = campaign.MetaData.Author;
            CampaignTags.Text = string.Join(',', campaign.MetaData.Tags);
            CampaignVersion.Text = campaign.MetaData.Version;
            CampaignExtraLives.Text = string.Join(',', campaign.MetaData.ExtraLivesMissions);
            CampaignVersion.Text = campaign.MetaData.Version;
            CampaignLoadingBGColor.Text = campaign.MetaData.BackgroundColor.ToString();
            CampaignLoadingStripColor.Text = campaign.MetaData.MissionStripColor.ToString();
            _hasMajorVictory = campaign.MetaData.HasMajorVictory;

            for (int i = 0; i < campaign.CachedMissions.Length; i++)
            {
                var mission = campaign.CachedMissions[i];
                var btn = new UITextButton(mission.Name, TankGame.TextFont, Color.White, () => Vector2.One.ToResolution());
                btn.SetDimensions(() => new Vector2(_missionButtonScissor.X + 15.ToResolutionX(), _missionButtonScissor.Y + _missionsOffset), () => new Vector2(_missionButtonScissor.Width - 30.ToResolutionX(), 25.ToResolutionY()));
                
                btn.Offset = new(0, i * 30);
                // _missionsMaxOff += btn.Offset.Y.ToResolutionY();
                btn.HasScissor = true;
                btn.Scissor = () => _missionButtonScissor;

                int index = i;

                btn.OnLeftClick = (a) =>
                {
                    _missionButtons.ForEach(x => x.Color = Color.White);
                    btn.Color = Color.SkyBlue;
                    _loadedCampaign.LoadMission(index);
                    _loadedCampaign.SetupLoadedMission(true);

                    MissionName.Text = _loadedCampaign.CachedMissions[index].Name;
                };
                _missionButtons.Add(btn);
            }
            UIElement.CunoSucks();
        }
        private static void RemoveMissionButtons()
        {
            for (int i = 0; i < _missionButtons.Count; i++)
                _missionButtons[i].Remove();
            _missionButtons.Clear();
        }
        public static void Open(bool fromMainMenu = true)
        {
            if (fromMainMenu)
            {
                IntermissionSystem.TimeBlack = 180;
                GameProperties.ShouldMissionsProgress = false;
                Task.Run(async () =>
                {
                    while (IntermissionSystem.BlackAlpha > 0.8f || MainMenu.Active)
                        await Task.Delay(TankGame.LogicTime).ConfigureAwait(false);

                    Active = true;
                    TankGame.OverheadView = true;
                    Theme.Play();
                    SetLevelEditorVisibility(true);
                });
            }
            else
            {
                Theme.Play();
                Active = true;
                SetLevelEditorVisibility(true);
            }
            Editing = true;
        }
        public static void Close()
        {
            Active = false;
            RemoveMissionButtons();

            Theme.SetVolume(0);
            Theme.Stop();
            SetLevelEditorVisibility(false);
            SetSaveMenuVisibility(false);
            // SetMissionsVisibility(false);
            _loadedCampaign = null;
            for (int i = 0; i < PlacementSquare.Placements.Count; i++) {
                PlacementSquare.Placements[i].TankId = -1;
                PlacementSquare.Placements[i].BlockId = -1;
            }
        }

        private static Rectangle _clickRect;

        private static readonly Dictionary<Rectangle, (int, string)> ClickEventsPerItem = new(); // hover zone, id, description

        private static string _curDescription = string.Empty;
        private static Rectangle _curHoverRect;

        private static readonly List<Color> TeamColors = new();

        public static Color SelectionColor = Color.NavajoWhite;
        public static Color HoverBoxColor = Color.SkyBlue;

        public static void Render()
        {
            if (!_initialized)
                return;

            ShouldDrawBarUI = !GameUI.Paused;
            SwapMenu.Text = _viewMissionDetails ? "Campaign Details" : "Mission Details";

            if (!ShouldDrawBarUI)
                return;

            #region Main UI
            int xOff = 0;
            _clickRect = new(0, (int)(GameUtils.WindowBottom.Y * 0.8f), GameUtils.WindowWidth, (int)(GameUtils.WindowHeight * 0.2f));
            TankGame.SpriteRenderer.Draw(TankGame.WhitePixel, _clickRect, null, Color.White, 0f, Vector2.Zero, default, 0f);

            var measure = TankGame.TextFont.MeasureString(_curDescription);

            if (_curDescription != null && _curDescription != string.Empty)
            {
                int padding = 20;
                var orig = new Vector2(0, TankGame.WhitePixel.Size().Y);
                TankGame.SpriteRenderer.Draw(TankGame.WhitePixel,
                    new Rectangle((int)(GameUtils.WindowWidth / 2 - (measure.X / 2 + padding).ToResolutionX()), (int)(GameUtils.WindowHeight * 0.8f), (int)(measure.X + padding * 2).ToResolutionX(), (int)(measure.Y + 20).ToResolutionY()), null, Color.White, 0f, orig, default, 0f);
            }

            // i feel like i could turn these panels into their own method.
            // but whatever.

            TankGame.SpriteRenderer.DrawString(TankGame.TextFont, _curDescription, new Vector2(GameUtils.WindowWidth / 2, GameUtils.WindowHeight * 0.78f), Color.Black, Vector2.One.ToResolution(), 0f, new Vector2(measure.X / 2, measure.Y));
            // level info
            TankGame.SpriteRenderer.Draw(TankGame.WhitePixel, new Rectangle(0, 0, 350, 125).ToResolution(), null, Color.Gray, 0f, Vector2.Zero, default, 0f);
            TankGame.SpriteRenderer.Draw(TankGame.WhitePixel, new Rectangle(0, 0, 350, 40).ToResolution(), null, Color.White, 0f, Vector2.Zero, default, 0f);
            TankGame.SpriteRenderer.DrawString(TankGame.TextFont, "Level Information", new Vector2(175, 3).ToResolution(), Color.Black, Vector2.One.ToResolution(), 0f, GameUtils.GetAnchor(Anchor.TopCenter, TankGame.TextFont.MeasureString("Level Information")));
            TankGame.SpriteRenderer.DrawString(TankGame.TextFont, $"Total Enemy Tanks: {AITank.CountAll()}", new Vector2(10, 40).ToResolution(), Color.White, Vector2.One.ToResolution(), 0f, Vector2.Zero);
            TankGame.SpriteRenderer.DrawString(TankGame.TextFont, $"Difficulty Rating: {DifficultyAlgorithm.GetDifficulty(Mission.GetCurrent()):0.00}", new Vector2(10, 80).ToResolution(), Color.White, Vector2.One.ToResolution(), 0f, Vector2.Zero);


            // render campaign missions ui 

            if (_loadedCampaign != null) {
                var heightDiff = 40;
                _missionButtonScissor = new Rectangle(_missionTab.X, _missionTab.Y + heightDiff, _missionTab.Width, _missionTab.Height - heightDiff).ToResolution();
                TankGame.SpriteRenderer.Draw(TankGame.WhitePixel, _missionTab.ToResolution(), null, Color.Gray, 0f, Vector2.Zero, default, 0f);
                TankGame.SpriteRenderer.Draw(TankGame.WhitePixel, new Rectangle(_missionTab.X, _missionTab.Y, _missionTab.Width, heightDiff).ToResolution(), null, Color.White, 0f, Vector2.Zero, default, 0f);
                TankGame.SpriteRenderer.DrawString(TankGame.TextFont, "Mission List", new Vector2(175, 153).ToResolution(), Color.Black, Vector2.One.ToResolution(), 0f, GameUtils.GetAnchor(Anchor.TopCenter, TankGame.TextFont.MeasureString("Mission List")));
            }
            // render teams

            // placement information
            TankGame.SpriteRenderer.Draw(TankGame.WhitePixel, new Rectangle(GameUtils.WindowWidth - (int)350.ToResolutionX(), 0, (int)350.ToResolutionX(), (int)500.ToResolutionY()), null, Color.Gray, 0f, Vector2.Zero, default, 0f);
            TankGame.SpriteRenderer.Draw(TankGame.WhitePixel, new Rectangle(GameUtils.WindowWidth - (int)350.ToResolutionX(), 0, (int)350.ToResolutionX(), (int)40.ToResolutionY()), null, Color.White, 0f, Vector2.Zero, default, 0f);
            TankGame.SpriteRenderer.DrawString(TankGame.TextFont, "Placement Information", new Vector2(GameUtils.WindowWidth - 325.ToResolutionX(), 3.ToResolutionY()), Color.Black, Vector2.One.ToResolution(), 0f, Vector2.Zero);
            if (CurCategory == Category.Blocks)
                TankGame.SpriteRenderer.DrawString(TankGame.TextFont, $"Block Stack: {BlockHeight}", new Vector2(GameUtils.WindowWidth - 335.ToResolutionX(), 40.ToResolutionY()), Color.White, Vector2.One.ToResolution(), 0f, Vector2.Zero);
            var helpText = "";
            Vector2 start = new();
            if (CurCategory == Category.EnemyTanks || CurCategory == Category.PlayerTanks)
            {
                helpText = "UP and DOWN to change teams.";
                start = new(GameUtils.WindowWidth - 250.ToResolutionX(), 140.ToResolutionY());

                TankGame.SpriteRenderer.DrawString(TankGame.TextFont, "Tank Teams", new Vector2(start.X + 45.ToResolutionX(), start.Y - 80.ToResolutionY()), Color.White, Vector2.One.ToResolution(), 0f, TankGame.TextFont.MeasureString("Tank Teams") / 2);

                TankGame.SpriteRenderer.Draw(TankGame.WhitePixel, new Rectangle((int)start.X, (int)(start.Y - 40.ToResolutionY()), (int)40.ToResolutionX(), (int)40.ToResolutionY()), null, Color.Black, 0f, Vector2.Zero, default, 0f);
                TankGame.SpriteRenderer.DrawString(TankGame.TextFont, "No Team", new Vector2(start.X + 45.ToResolutionX(), start.Y - 40.ToResolutionY()), Color.Black, Vector2.One.ToResolution(), 0f, Vector2.Zero);
                for (int i = 0; i < TeamID.Collection.Count - 1; i++)
                {
                    var color = TeamColors[i];
                    TankGame.SpriteRenderer.DrawString(TankGame.TextFont, TeamID.Collection.GetKey(i + 1), new Vector2(start.X + 45.ToResolutionX(), start.Y + (i * 40).ToResolutionY()), color, Vector2.One.ToResolution(), 0f, Vector2.Zero); ;
                    TankGame.SpriteRenderer.Draw(TankGame.WhitePixel, new Rectangle((int)start.X, (int)(start.Y + (i * 40).ToResolutionY()), (int)40.ToResolutionX(), (int)40.ToResolutionY()), null, color, 0f, Vector2.Zero, default, 0f);
                }
                TankGame.SpriteRenderer.DrawString(TankGame.TextFontLarge, ">", new Vector2(start.X - 25.ToResolutionX(), start.Y + ((int)(SelectedTankTeam - 1) * 40).ToResolutionY()), Color.White, Vector2.One.ToResolution(), 0f, TankGame.TextFontLarge.MeasureString(">") / 2);

                if (SelectedTankTeam != TeamID.Magenta)
                    TankGame.SpriteRenderer.DrawString(TankGame.TextFont, "v", new Vector2(start.X - 25.ToResolutionX(), start.Y + ((int)(SelectedTankTeam - 1) * 40 + 50).ToResolutionY()), Color.White, Vector2.One.ToResolution(), 0f, TankGame.TextFont.MeasureString("v") / 2);
                if (SelectedTankTeam != TeamID.NoTeam)
                    TankGame.SpriteRenderer.DrawString(TankGame.TextFont, "v", new Vector2(start.X - 25.ToResolutionX(), start.Y + ((int)(SelectedTankTeam - 1) * 40 - 10).ToResolutionY()), Color.White, Vector2.One.ToResolution(), MathHelper.Pi, TankGame.TextFont.MeasureString("v") / 2);
            }
            else if (CurCategory == Category.Blocks)
            {
                helpText = "UP and DOWN to change stack.";
                // TODO: add static dict for specific types?
                var tex = SelectedBlockType != BlockID.Hole ? $"{BlockID.Collection.GetKey(SelectedBlockType)}_{BlockHeight}" : $"{BlockID.Collection.GetKey(SelectedBlockType)}";
                var size = RenderTextures[tex].Size();
                start = new Vector2(GameUtils.WindowWidth - 175.ToResolutionX(), 450.ToResolutionY());
                TankGame.SpriteRenderer.Draw(RenderTextures[tex], start, null, Color.White, 0f, new Vector2(size.X / 2, size.Y), Vector2.One.ToResolution(), default, 0f);
                // TODO: reduce the hardcode for modders, yeah
                if (SelectedBlockType != BlockID.Teleporter && SelectedBlockType != BlockID.Hole)
                {
                    TankGame.SpriteRenderer.DrawString(TankGame.TextFontLarge, "v", new Vector2(start.X + 100.ToResolutionX(), start.Y - 75.ToResolutionY()), Color.White, Vector2.One.ToResolution(), 0f, TankGame.TextFontLarge.MeasureString("v") / 2);
                    TankGame.SpriteRenderer.DrawString(TankGame.TextFontLarge, "v", new Vector2(start.X - 100.ToResolutionX(), start.Y - 25.ToResolutionY()), Color.White, Vector2.One.ToResolution(), MathHelper.Pi, TankGame.TextFontLarge.MeasureString("v") / 2);
                }
            }
            TankGame.SpriteRenderer.DrawString(TankGame.TextFont, helpText, new Vector2(GameUtils.WindowWidth - 175.ToResolutionX(), GameUtils.WindowHeight / 2 - 70.ToResolutionY()), Color.White, new Vector2(0.5f).ToResolution(), 0f, TankGame.TextFont.MeasureString(helpText) / 2);
            TankGame.SpriteRenderer.Draw(TankGame.WhitePixel, _curHoverRect, null, HoverBoxColor * 0.5f, 0f, Vector2.Zero, default, 0f);

            if (CurCategory == Category.EnemyTanks)
            {
                for (int i = 0; i < _renderNamesTanks.Count; i++)
                {
                    ClickEventsPerItem[new Rectangle((int)(34.ToResolutionX() + xOff + _barOffset), (int)(GameUtils.WindowBottom.Y * 0.8f), (int)234.ToResolutionX(), (int)(GameUtils.WindowHeight * 0.2f))] =
                        (i + 2, (i + 2) switch
                        {
                            TankID.Brown => "An easy to defeat, stationary, slow firing enemy.",
                            TankID.Ash => "Similar to the Brown tank, but can move slowly and fire more often.",
                            TankID.Marine => "A slow-moving, passive and methodical enemy.\nShoots off rockets instead of standard shells.",
                            TankID.Yellow => "Not incredibly dangerous. Lays mines often and move fast.",
                            TankID.Pink => "A slow, but incredibly persistent and aggressive tank.\nCan fire multiple bullets at once.",
                            TankID.Green => "A highly-dangerous but stationary tank.\nShoots multiple rockets that can bounce twice.",
                            TankID.Violet => "A fast-moving, intelligent tank that can spray\nup to 5 bullets at once and can lay mines.",
                            TankID.White => "A slow-moving, powerful tank that turns invisible\nat the start of the mission and can fire multiple bullets and lay mines.",
                            TankID.Black => "A tank that moves faster than the player, fires rockets often,\nis aggressive, and can dodge well. Can lay mines.",
                            TankID.Bronze => "A simple, stationary tank that can fire multiple bullets\nand aims directly at its target.",
                            TankID.Silver => "An advanced and difficult mobile tank that can fire up\nto 8 bullets at fast rates and can dodge well. Can lay mines",
                            TankID.Sapphire => "A deadly tank that can rapidly fire up to 3 rockets\nin a single volley. Can lay mines.",
                            TankID.Ruby => "A slow, but very aggressive tank that constantly fires shells.",
                            TankID.Citrine => "An insanely fast tank that shoots very fast shells at its target.\nLays mines frequently.",
                            TankID.Amethyst => "A fast, very agile, and dodgy tank that fires a spread\nof shells at its target. Can lay mines.",
                            TankID.Emerald => "A stationary tank that turns invisible at the start of the round.\nFires multiple double-bounce rockets at its target.",
                            TankID.Gold => "A slow and mobile tank that turns invisible at the start of the round\nand lays no tracks for players to see. Can lay mines.",
                            TankID.Obsidian => "A very fast, but very unintelligent tank that fires\nfast rockets frequently with 2 bounces. Can lay mines.",
                            TankID.Granite => "A very slow, mobile tank that becomes stunned\n for a while upon firing. Shoots faster-than-normal shells.",
                            TankID.Bubblegum => "A medium-speed, fast-firing dodgy tank that can lay mines.",
                            TankID.Water => "A medium-speed tank that moves linearly.\nFires rockets that bounce one time, and can also lay mines.",
                            TankID.Crimson => "A slow tank that fires in a burst of 5 shells. Can lay mines.",
                            TankID.Tiger => "A very wary tank that strafes very often and dodges very often.\nFires shells fast and lays mines often.",
                            TankID.Fade => "A tank that is mainly focused on dodging threats.\nFires often and can lay mines.",
                            TankID.Creeper => "A highly dangerous tank that fires very fast rockets\n that bounce 3 times. Moves very slowly.",
                            TankID.Gamma => "A stationary tank that fires insanely fast bullets at rapid frequency.",
                            TankID.Marble => "An apex predator tank that is good at dodging, good at\ncalculating shots, is fast, and fires fast rockets rapidly.",
                            _ => "What?"
                        }); // TODO: localize this. i hate english.

                    TankGame.SpriteRenderer.Draw(RenderTextures[_renderNamesTanks[i]], new Vector2(24.ToResolutionX() + xOff + _barOffset, GameUtils.WindowBottom.Y * 0.75f), null, (int)SelectedTankTier - 2 == i ? SelectionColor : Color.White, 0f, Vector2.Zero, Vector2.One.ToResolution(), default, 0f);
                    xOff += (int)234.ToResolutionX();
                }
                _maxScroll = xOff;
            }
            else if (CurCategory == Category.Blocks)
            {
                for (int i = 0; i < _renderNamesBlocks.Count; i++)
                {
                    ClickEventsPerItem[new Rectangle((int)(34.ToResolutionX() + xOff + _barOffset), (int)(GameUtils.WindowBottom.Y * 0.8f), (int)234.ToResolutionX(), (int)(GameUtils.WindowHeight * 0.2f))] =
                        (i, i switch
                        {
                            BlockID.Wood => "An indestructible obstacle.",
                            BlockID.Cork => "An obstacle that can be destroyed by mines.",
                            BlockID.Hole => "An obstacle that tanks cannot travel through, but shells can.",
                            _ => "What?"
                        });

                    TankGame.SpriteRenderer.Draw(RenderTextures[_renderNamesBlocks[i]], new Vector2(24.ToResolutionX() + xOff + _barOffset, GameUtils.WindowBottom.Y * 0.75f), null, (int)SelectedBlockType == i ? SelectionColor : Color.White, 0f, Vector2.Zero, Vector2.One.ToResolution(), default, 0f);
                    xOff += (int)234.ToResolutionX();
                }
                _maxScroll = xOff;
            }
            else if (CurCategory == Category.PlayerTanks)
            {
                for (int i = 0; i < _renderNamesPlayers.Count; i++)
                {
                    ClickEventsPerItem[new Rectangle((int)(34.ToResolutionX() + xOff + _barOffset), (int)(GameUtils.WindowBottom.Y * 0.8f), (int)234.ToResolutionX(), (int)(GameUtils.WindowHeight * 0.2f))] =
                        (i, i switch
                        {
                            PlayerID.Blue => "The blue player tank (P1)",
                            PlayerID.Red => "The red player tank. (P2)",
                            _ => "What?"
                        });

                    TankGame.SpriteRenderer.Draw(RenderTextures[_renderNamesPlayers[i]], new Vector2(24.ToResolutionX() + xOff + _barOffset, GameUtils.WindowBottom.Y * 0.75f), null, (int)SelectedPlayerType == i ? SelectionColor : Color.White, 0f, Vector2.Zero, Vector2.One.ToResolution(), default, 0f);
                    xOff += (int)234.ToResolutionX();
                }
                _maxScroll = xOff;
            }
            if (DebugUtils.DebuggingEnabled)
            {
                int a = 0;
                foreach (var thing in ClickEventsPerItem)
                {
                    if (DebugUtils.DebugLevel == 3)
                        TankGame.SpriteRenderer.Draw(TankGame.WhitePixel, thing.Key, null, Color.Red * 0.5f, 0f, Vector2.Zero, default, 0f);
                    var text = thing.Key.Contains(GameUtils.MousePosition.ToPoint()) ? $"{thing.Key} ---- {thing.Value.Item1} (HOVERED)" : $"{thing.Key} ---- {thing.Value.Item1}";
                    DebugUtils.DrawDebugString(TankGame.SpriteRenderer, text, new Vector2(500, 20 + a), 3);
                    a += 20;
                }
            }
            #endregion
            // TankGame.SpriteRenderer.Draw(TankGame.WhitePixel, _missionButtonScissor, null, Color.Red * 0.5f, 0f, Vector2.Zero, default, 0f);

            if (Active && TankGame.HoveringAnyTank)
            {
                var tex = GameResources.GetGameResource<Texture2D>("Assets/textures/ui/leveledit/rotate");
                TankGame.SpriteRenderer.Draw(tex,
                   GameUtils.MousePosition + new Vector2(20, -20).ToResolution(), null, Color.White, 0f, new Vector2(0, tex.Size().Y), 0.2f.ToResolution(), default, 0f);
            }
            if (Active && GUICategory == UICategory.SavingThings)
                TankGame.SpriteRenderer.Draw(TankGame.WhitePixel,
                   LevelContentsPanel, null, Color.Gray, 0f, Vector2.Zero, default, 0f);
            var txt = !_viewMissionDetails ? "Campaign Details" : "Mission Details";

            if (GUICategory == UICategory.SavingThings)
                TankGame.SpriteRenderer.DrawString(TankGame.TextFont, txt, new Vector2(LevelContentsPanel.X + LevelContentsPanel.Width / 2, LevelContentsPanel.Y + 10.ToResolutionY()), Color.White, Vector2.One.ToResolution(), 0f, new Vector2(TankGame.TextFont.MeasureString(txt).X / 2, 0));

            _campaignElems.ForEach(elem =>
            {
                if (elem is UITextInput)
                    elem.DrawText = false;
                elem.UniqueDraw = (a, b) =>
                {
                    if (!elem.IsVisible)
                        return;
                    // fix why this isn't drawing???
                    var pos = new Vector2(elem.Position.X + 10.ToResolutionX(), elem.Position.Y + elem.Size.Y / 2);
                    //var pos = Vector2.Zero;
                    string text = elem.DefaultString + ": " + elem.GetRealText();
                    var msr1 = TankGame.TextFontLarge.MeasureString(text);
                    var msr2 = TankGame.TextFontLarge.MeasureString(elem.DefaultString);
                    float constScale = 0.4f.ToResolutionX();
                    float scale = /*elem.DefaultString.Length < 30 ? 
                    msr2.X / msr1.X * 0.5f :
                    msr2.X / msr1.X * 0.3f;*/ msr1.X * constScale > elem.Size.X ? msr2.X / (msr1.X + msr2.X) : constScale;
                    b.DrawString(TankGame.TextFontLarge, text, pos, Color.Black, new Vector2(scale).ToResolution(), 0f, new Vector2(0, msr1.Y / 2));
                };
            });
        }

        public static void Update()
        {
            if (!_initialized)
                return;

            _missionsOffset += Input.GetScrollWheelChange() * 30;

            _missionsMaxOff = _missionButtons.Count * 25.ToResolutionY();

            CampaignMajorVictory.Text = "Has Major Victory Theme: " + (_hasMajorVictory ? "Yes" : "No");

            if (_missionsOffset > 0)
                _missionsOffset = 0;
            else if (_missionsOffset < -_missionsMaxOff)
                _missionsOffset = -_missionsMaxOff;

            LevelContentsPanel = new Rectangle(GameUtils.WindowWidth / 4, (int)(GameUtils.WindowHeight * 0.1f), GameUtils.WindowWidth / 2, (int)(GameUtils.WindowHeight * 0.625f));
            PlacementSquare.PlacesBlock = CurCategory == Category.Blocks;
            switch (CurCategory) {
                case Category.EnemyTanks:
                    EnemyTanksCategory.Color = Color.DeepSkyBlue;
                    BlocksCategory.Color = Color.White;
                    PlayerTanksCategory.Color = Color.White;
                    break;
                case Category.Blocks:
                    EnemyTanksCategory.Color = Color.White;
                    BlocksCategory.Color = Color.DeepSkyBlue;
                    PlayerTanksCategory.Color = Color.White;
                    break;
                case Category.PlayerTanks:
                    EnemyTanksCategory.Color = Color.White;
                    BlocksCategory.Color = Color.White;
                    PlayerTanksCategory.Color = Color.DeepSkyBlue;
                    break;
            }
            if (Active)
            {
                Theme.SetVolume(0.4f * TankGame.Settings.MusicVolume);

                _curDescription = string.Empty;

                _curHoverRect = new();
                foreach (var thing in ClickEventsPerItem)
                {
                    if (thing.Key.Contains(GameUtils.MousePosition.ToPoint()))
                    {
                        _curHoverRect = thing.Key;
                        if (thing.Value.Item2 != null)
                            _curDescription = thing.Value.Item2;
                    }
                }

                if (Input.CanDetectClick())
                {
                    _origClick = GameUtils.MousePosition - new Vector2(_barOffset, 0);

                    for (int i = 0; i < ClickEventsPerItem.Count; i++)
                    {
                        var evt = ClickEventsPerItem.ElementAt(i);
                        if (evt.Key.Contains(GameUtils.MousePosition.ToPoint()))
                        {
                            if (CurCategory == Category.EnemyTanks)
                                SelectedTankTier = evt.Value.Item1;
                            else if (CurCategory == Category.Blocks)
                                SelectedBlockType = evt.Value.Item1;
                            else if (CurCategory == Category.PlayerTanks)
                                SelectedPlayerType = evt.Value.Item1;
                        }
                    }
                }
                if (Input.MouseLeft && _clickRect.Contains(GameUtils.MousePosition.ToPoint()))
                {
                    _barOffset = GameUtils.MousePosition.X - _origClick.X;
                    if (_barOffset < -_maxScroll + GameUtils.WindowWidth - 60.ToResolutionX())
                        _barOffset = -_maxScroll + GameUtils.WindowWidth - 60.ToResolutionX();
                    if (_barOffset > 0)
                    {
                        _barOffset = 0;
                        _origClick = GameUtils.MousePosition - new Vector2(_barOffset, 0);
                    }
                }

                BlockHeight = MathHelper.Clamp(BlockHeight, 1, 7);

                if (CurCategory == Category.EnemyTanks || CurCategory == Category.PlayerTanks)
                {
                    // tank place handling, etc
                    if (Input.KeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Up))
                        SelectedTankTeam--;
                    if (Input.KeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Down))
                        SelectedTankTeam++;
                    if (SelectedTankTeam > TeamID.Magenta)
                        SelectedTankTeam = TeamID.Magenta;
                    if (SelectedTankTeam < TeamID.NoTeam)
                        SelectedTankTeam = TeamID.NoTeam;
                }
                else if (CurCategory == Category.Blocks)
                {
                    if (Input.KeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Up))
                        BlockHeight++;
                    if (Input.KeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Down))
                        BlockHeight--;
                    if (SelectedBlockType == BlockID.Hole || SelectedBlockType == BlockID.Teleporter)
                        BlockHeight = 1;

                    BlockHeight = MathHelper.Clamp(BlockHeight, 1, 7);
                }
                ClickEventsPerItem.Clear();
            }
            else if (Editing && !Active && _cachedMission != default && GameProperties.InMission)
                if (GameHandler.NothingCanHappenAnymore(_cachedMission, out bool victory))
                    QueueEditorReEntry(TimeSpan.FromSeconds(2), true);
            if (ReturnToEditor != null)
                ReturnToEditor.IsVisible = Editing && !Active && !MainMenu.Active;
        }

        private static void QueueEditorReEntry(TimeSpan delay, bool instant)
        {
            if (instant)
                ReturnToEditor?.OnLeftClick?.Invoke(null);
            Task.Run(async () =>
            {
                await Task.Delay(delay).ConfigureAwait(false);
                ReturnToEditor?.OnLeftClick?.Invoke(null);
            });
        }
    }
}
