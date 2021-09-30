﻿using OriDE.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace OriDETracker
{
    public partial class Tracker : Form
    {
        public Tracker()
        {
            InitializeComponent();

            // Apply settings options for Refresh Rate (and refresh_time) 
            RefreshRate = TrackerSettings.Default.RefreshRate;

            // Apply settings options Tracker Size using UpdateTrackerSize
            UpdateTrackerSize(TrackerSettings.Default.Pixels);

            // Settings options for what to track
            track_shards = TrackerSettings.Default.Shards;
            track_relics = TrackerSettings.Default.Relics;
            track_teleporters = TrackerSettings.Default.Teleporters;
            track_trees = TrackerSettings.Default.Trees;
            track_mapstones = TrackerSettings.Default.Mapstones;

            // Settings for what to display
            display_empty_relics = TrackerSettings.Default.DisplayEmptyRelics;
            display_empty_trees = TrackerSettings.Default.DisplayEmptyTrees;
            display_empty_teleporters = TrackerSettings.Default.DisplayEmptyTeleporters;

            // Load the default logic options, bitfields, and mouse mappings
            SetDefaults();

            // Load settings for this form
            this.MoveToolStripMenuItem.Checked = TrackerSettings.Default.Draggable;
            this.AutoUpdateToolStripMenuItem.Checked = TrackerSettings.Default.AutoUpdate;
            this.AlwaysOnTopToolStripMenuItem.Checked = TrackerSettings.Default.AlwaysOnTop;
            this.TopMost = TrackerSettings.Default.AlwaysOnTop;

            // Other Settings
            var fontColor = TrackerSettings.Default.MapFontColor;
            var fontFamilyName = TrackerSettings.Default.MapFontFamilyName;
            Opacity = TrackerSettings.Default.Opacity;
            BackColor = TrackerSettings.Default.Background;

            // Auto update boolean values
            auto_update = TrackerSettings.Default.AutoUpdate;

            if (fontColor == null)
                fontColor = Color.White;
            if (BackColor == null)
                BackColor = Color.Black;

            var font_brush = new SolidBrush(fontColor);

            bool need_font, found_font = false;

            if (string.IsNullOrEmpty(fontFamilyName))
                need_font = true;
            else
                need_font = false;

            if (need_font)
                // first looks for amatic sc
                foreach (FontFamily ff in FontFamily.Families)
                {
                    if (ff.Name.ToLower() == "amatic sc")
                    {
                        fontFamilyName = "Amatic SC";
                        found_font = true;
                        break;
                    }
                }
            // if not found then ask for a font
            if (need_font && !found_font)
            {
                MessageBox.Show("It is recommended to install and use the included fonts: Amatic SC and Amatic SC Bold");
                if (this.MapStoneFontDialog.ShowDialog() == DialogResult.OK)
                {
                    fontFamilyName = MapStoneFontDialog.Font.FontFamily.Name;
                }
                else
                {
                    fontFamilyName = FontFamily.GenericSansSerif.Name;
                }
            }
            // finally load font
            mapstoneFont = MapstoneFontFactory.Create(TrackerSize, fontFamilyName, font_brush);

            // Initialize the OriMemory module that Devil/Eiko/Sigma wrote
            Mem = new OriMemory();
            trackerBitfields = MemoryBitfields.Empty;

            // Initialize background update loop
            th = new Thread(UpdateLoop)
            {
                IsBackground = true
            };

            // Settings window display
            settings = new SettingsLayout(this)
            {
                Visible = false
            };
        }

        #region PrivateVariables
        protected static int PIXEL_DEF = 667;
        protected int image_pixel_size;

        protected TrackerPixelSizes tracker_size;

        private MapstoneFont mapstoneFont;

        protected AutoUpdateRefreshRates refresh_rate;
        protected int refresh_time;

        protected bool mode_shards;
        protected bool mode_force_trees;
        protected bool mode_world_tour;
        protected bool mode_warmth_fragments;
        protected bool mode_force_maps;

        protected int current_frags;
        protected int max_frags;

        protected bool draggable = TrackerSettings.Default.Draggable;
        protected bool auto_update = TrackerSettings.Default.AutoUpdate;

        protected bool track_teleporters = TrackerSettings.Default.Teleporters;
        protected bool track_trees = TrackerSettings.Default.Trees;
        protected bool track_shards = TrackerSettings.Default.Shards;
        protected bool track_relics = TrackerSettings.Default.Relics;
        protected bool track_mapstones = TrackerSettings.Default.Mapstones;

        protected bool display_empty_relics = TrackerSettings.Default.DisplayEmptyRelics;
        protected bool display_empty_trees = TrackerSettings.Default.DisplayEmptyTrees;
        protected bool display_empty_teleporters = TrackerSettings.Default.DisplayEmptyTeleporters;
        protected bool display_empty_shards = TrackerSettings.Default.DisplayEmptyShards;

        protected OriMemory Mem { get; set; }
        protected MemoryBitfields trackerBitfields;

        protected Thread th;
        protected SettingsLayout settings;

        private readonly string[] skill_list = { "Spirit Flame", "Wall Jump", "Charge Flame", "Double Jump", "Bash", "Stomp", "Glide", "Climb", "Charge Jump", "Grenade", "Dash" };
        private readonly string[] event_list = { "Water Vein", "Gumon Seal", "Sunstone", "Clean Water", "Wind Restored" };
        private readonly string[] zone_list = { "Glades", "Grove", "Grotto", "Ginso", "Swamp", "Valley", "Misty", "Blackroot", "Sorrow", "Forlorn", "Horu" };
        private readonly string[] teleporter_list = { "Valley", "Misty", "Forlorn", "Sorrow", "Horu", "Blackroot", "Glades", "Grove", "Grotto", "Ginso", "Swamp" };
        private readonly string[] relic_list = { "Valley", "Misty", "Forlorn", "Sorrow", "Horu", "Blackroot", "Glades", "Grove", "Grotto", "Ginso", "Swamp" };
        #endregion

        #region PublicProperties
        internal MapstoneFont MapstoneFont
        {
            get { return mapstoneFont; }
            set { mapstoneFont = value; }
        }
        public TrackerPixelSizes TrackerSize
        {
            get { return tracker_size; }
            set { tracker_size = value; image_pixel_size = (int)value; }
        }

        public AutoUpdateRefreshRates RefreshRate
        {
            get { return refresh_rate; }
            set { refresh_rate = value; refresh_time = (int)(1000000.0f / ((float)value)); }
        }

        public bool TrackShards
        {
            get { return track_shards; }
            set { track_shards = value; this.Refresh(); }
        }
        public bool TrackTeleporters
        {
            get { return track_teleporters; }
            set { track_teleporters = value; this.Refresh(); }
        }
        public bool TrackTrees
        {
            get { return track_trees; }
            set { track_trees = value; this.Refresh(); }
        }
        public bool TrackRelics
        {
            get { return track_relics; }
            set { track_relics = value; this.Refresh(); }
        }
        public bool TrackMapstones
        {
            get { return track_mapstones; }
            set { track_mapstones = value; this.Refresh(); }
        }
        public int MapstoneCount
        {
            get { return mapstone_count; }
            set { mapstone_count = value; }
        }

        public bool DisplayEmptyRelics
        {
            get { return display_empty_relics; }
            set { display_empty_relics = value; this.Refresh(); }
        }
        public bool DisplayEmptyTrees
        {
            get { return display_empty_trees; }
            set { display_empty_trees = value; this.Refresh(); }
        }
        public bool DisplayEmptyTeleporters
        {
            get { return display_empty_teleporters; }
            set { display_empty_teleporters = value; this.Refresh(); }
        }
        public bool DisplayEmptyShards
        {
            get { return display_empty_shards; }
            set { display_empty_shards = value; this.Refresh(); }
        }
        #endregion

        #region FrameMoving
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        #endregion

        #region LogicDictionary
        // not a dictionary but "logic"
        protected int mapstone_count = 0;

        //Skills, Trees, Events, Shards, Teleporters, and Relics
        protected ConcurrentDictionary<String, bool> haveSkill = new ConcurrentDictionary<string, bool>();
        protected ConcurrentDictionary<String, bool> haveTree = new ConcurrentDictionary<string, bool>();
        protected ConcurrentDictionary<String, bool> haveEvent = new ConcurrentDictionary<string, bool>();
        protected ConcurrentDictionary<String, bool> haveShards = new ConcurrentDictionary<string, bool>();
        protected ConcurrentDictionary<String, bool> haveTeleporters = new ConcurrentDictionary<string, bool>();
        protected ConcurrentDictionary<String, bool> relicExists = new ConcurrentDictionary<string, bool>();
        protected ConcurrentDictionary<String, bool> relicFound = new ConcurrentDictionary<string, bool>();

        //Bits
        private Dictionary<String, int> treeBits;
        private Dictionary<String, int> skillBits;
        private Dictionary<String, int> teleporterBits;
        private Dictionary<String, int> mapstoneBits;
        private Dictionary<String, int> relicExistsBits;
        private Dictionary<String, int> relicFoundBits;
        #endregion

        #region Images
        protected String DIR = @"Assets_667/";

        protected Image imageSkillWheelDouble;
        protected Image imageGSkills;
        protected Image imageGTrees;
        protected Image imageGTeleporters;
        protected Image imageGShards;
        protected Image imageMapStone;

        protected Dictionary<String, Image> skillImages = new Dictionary<String, Image>();
        protected Dictionary<String, Image> treeImages = new Dictionary<String, Image>();
        protected Dictionary<String, Image> eventImages = new Dictionary<String, Image>();
        protected Dictionary<String, Image> eventGreyImages = new Dictionary<String, Image>();
        protected Dictionary<String, Image> shardImages = new Dictionary<string, Image>();
        protected Dictionary<String, Image> teleporterImages = new Dictionary<String, Image>();
        protected Dictionary<String, Image> relicExistImages = new Dictionary<String, Image>();
        protected Dictionary<String, Image> relicFoundImages = new Dictionary<String, Image>();

        private void DisposeImages()
        {
            imageSkillWheelDouble?.Dispose();
            imageGSkills?.Dispose();
            imageGTrees?.Dispose();
            imageGTeleporters?.Dispose();
            imageGShards?.Dispose();
            imageMapStone?.Dispose();

            foreach (string skill in skill_list)
            {
                skillImages[skill]?.Dispose();
                treeImages[skill]?.Dispose();
            }

            foreach (string ev in event_list)
            {
                eventImages[ev]?.Dispose();
                eventGreyImages[ev]?.Dispose();

                if (ev == "Water Vein" || ev == "Gumon Seal" || ev == "Sunstone")
                {
                    shardImages[ev + " 1"]?.Dispose();
                    shardImages[ev + " 2"]?.Dispose();
                }
            }

            foreach (string zone in zone_list)
            {
                relicExistImages[zone]?.Dispose();
                relicFoundImages[zone]?.Dispose();

                if (zone != "Misty")
                {
                    teleporterImages[zone]?.Dispose();
                }
            }
        }

        private void UpdateImages()
        {
            // On startup, no tracker images are stored in dictionaries
            if (imageSkillWheelDouble != null)
            {
                DisposeImages();
            }

            DIR = "Assets_" + image_pixel_size.ToString() + @"/";

            imageSkillWheelDouble = Image.FromFile(DIR + @"SkillRing_Double.png");
            imageGSkills = Image.FromFile(DIR + @"GreySkillTree.png");
            imageGTrees = Image.FromFile(DIR + @"GreyTrees.png");
            imageGTeleporters = Image.FromFile(DIR + @"GreyTeleporters.png");
            imageGShards = Image.FromFile(DIR + @"GreyShards.png");
            imageMapStone = Image.FromFile(DIR + @"MapStone.png");

            foreach (string skill in skill_list)
            {
                skillImages[skill] = Image.FromFile(DIR + skill.Replace(" ", String.Empty) + @".png");
                treeImages[skill] = Image.FromFile(DIR + "T" + skill.Replace(" ", String.Empty) + @".png");
            }

            foreach (string ev in event_list)
            {
                eventImages[ev] = Image.FromFile(DIR + ev.Replace(" ", String.Empty) + @".png");
                eventGreyImages[ev] = Image.FromFile(DIR + "G" + ev.Replace(" ", String.Empty) + @".png");

                if (ev == "Water Vein" || ev == "Gumon Seal" || ev == "Sunstone")
                {
                    shardImages[ev + " 1"] = Image.FromFile(DIR + ev.Replace(" ", String.Empty) + @"Shard1.png");
                    shardImages[ev + " 2"] = Image.FromFile(DIR + ev.Replace(" ", String.Empty) + @"Shard2.png");
                }
            }

            foreach (string zone in zone_list)
            {
                relicExistImages[zone] = Image.FromFile(DIR + "Relics/Exist/" + zone + ".png");
                relicFoundImages[zone] = Image.FromFile(DIR + "Relics/Found/" + zone + ".png");

                if (zone != "Misty")
                {
                    teleporterImages[zone] = Image.FromFile(DIR + zone + "TP.png");
                }
            }
        }
        #endregion

        #region SetLayout
        //points for mouse clicks (with certain tolerance defined by TOL)
        private const int TOL = 25;
        private Point mapstoneMousePoint = new Point(333, 380);
        private readonly Dictionary<String, Point> eventMousePoint = new Dictionary<String, Point>();
        private readonly Dictionary<String, Point> treeMouseLocation = new Dictionary<String, Point>();
        private readonly Dictionary<String, Point> skillMousePoint = new Dictionary<String, Point>();
        private readonly Dictionary<String, Point> teleporterMouseLocation = new Dictionary<String, Point>();
        private readonly Dictionary<String, Point> relicMouseLocation = new Dictionary<String, Point>();
        private readonly Dictionary<String, Point> shardsMouseLocation = new Dictionary<String, Point>();

        private void SetDefaults()
        {
            SetMouseLocations();
            SetBitDefaults();
            SetSkillDefaults();
            SetEventDefaults();
            SetRelicDefaults();
            SetTeleportersDefaults();
        }
        private void SetSkillDefaults()
        {
            //haveTree and haveSkill Dictionaries
            foreach (var sk in skill_list)
            {
                haveTree[sk] = false;
                haveSkill[sk] = false;
            }
        }
        private void SetEventDefaults()
        {
            //haveEvent and haveShard Dictionaries
            foreach (var ev in event_list)
            {
                haveEvent[ev] = false;
                if (ev == "Water Vein" || ev == "Gumon Seal" || ev == "Sunstone")
                {
                    haveShards[ev + " 1"] = false;
                    haveShards[ev + " 2"] = false;
                }
            }
        }
        private void SetRelicDefaults()
        {
            //relicExists and relicFound dictionaries
            foreach (var zn in zone_list)
            {
                relicExists[zn] = true;
                relicFound[zn] = false;
            }
        }
        private void SetTeleportersDefaults()
        {
            foreach (var tp in teleporter_list)
            {
                if (tp == "Misty")
                {
                    continue;
                }

                haveTeleporters[tp] = false;
            }
        }
        private void SetBitDefaults()
        {
            #region Bits
            treeBits = new Dictionary<string, int>() {
                { "Spirit Flame", 0},
                { "Wall Jump", 1},
                { "Charge Flame", 2},
                { "Double Jump", 3},
                { "Bash", 4},
                { "Stomp", 5},
                { "Glide", 6},
                { "Climb", 7},
                { "Charge Jump", 8},
                { "Grenade", 9},
                { "Dash", 10}
            };
            skillBits = new Dictionary<string, int>() {
                { "Spirit Flame", 11},
                { "Wall Jump", 12},
                { "Charge Flame", 13},
                { "Double Jump", 14},
                { "Bash", 15},
                { "Stomp", 16},
                { "Glide", 17},
                { "Climb", 18},
                { "Charge Jump", 19},
                { "Grenade", 20},
                { "Dash", 21}
            };
            relicFoundBits = new Dictionary<string, int>() {
                {"Glades", 0},
                {"Grove", 1},
                {"Grotto", 2},
                {"Blackroot", 3},
                {"Swamp", 4},
                {"Ginso", 5},
                {"Valley", 6},
                {"Misty", 7},
                {"Forlorn", 8},
                {"Sorrow", 9},
                {"Horu", 10}
            };
            relicExistsBits = new Dictionary<string, int>() {
                {"Glades", 11},
                {"Grove", 12},
                {"Grotto", 13},
                {"Blackroot", 14},
                {"Swamp", 15},
                {"Ginso", 16},
                {"Valley", 17},
                {"Misty", 18},
                {"Forlorn", 19},
                {"Sorrow", 20},
                {"Horu", 21}
            };
            mapstoneBits = new Dictionary<string, int>()
            {
                {"Glades", 0},
                {"Blackroot", 1},
                {"Grove", 2},
                {"Grotto", 3},
                {"Swamp", 4},
                {"Valley", 5},
                {"Forlorn", 6},
                {"Sorrow", 7},
                {"Horu", 8},
            };
            teleporterBits = new Dictionary<string, int>()
            {
                {"Grove", 0},
                {"Swamp", 1},
                {"Grotto", 2},
                {"Valley", 3},
                {"Forlorn", 4},
                {"Sorrow", 5},
                {"Ginso", 6},
                {"Horu", 7},
                {"Blackroot", 8},
                {"Glades", 9}
            };
            #endregion
        }
        private void SetMouseLocations()
        {
            for (int i = 0; i < 11; i++)
            {
                skillMousePoint.Add(skill_list[i], new Point((int)(320 + 13 + 205 * Math.Sin(2.0 * i * Math.PI / 11.0)),
                                                         (int)(320 + 13 - 205 * Math.Cos(2.0 * i * Math.PI / 11.0))));

                treeMouseLocation.Add(skill_list[i], new Point((int)(320 + 13 + 286 * Math.Sin(2.0 * i * Math.PI / 11.0)),
                                                           (int)(320 + 13 - 286 * Math.Cos(2.0 * i * Math.PI / 11.0))));

                relicMouseLocation.Add(relic_list[i], new Point((int)(320 + 13 + 300 * -Math.Sin(2.0 * i * Math.PI / 11.0)),
                                                            (int)(320 + 13 - 300 * -Math.Cos(2.0 * i * Math.PI / 11.0))));

                if (teleporter_list[i] == "Misty")
                {
                    continue;
                }

                teleporterMouseLocation.Add(teleporter_list[i], new Point((int)(320 + 13 + 240 * -Math.Sin(2.0 * i * Math.PI / 11.0)),
                                                                        (int)(320 + 13 - 240 * -Math.Cos(2.0 * i * Math.PI / 11.0))));
            }


            eventMousePoint.Add("Water Vein", new Point(221 + 13, 258 + 13));
            eventMousePoint.Add("Gumon Seal", new Point(320 + 13, 215 + 13));
            eventMousePoint.Add("Sunstone", new Point(428 + 13, 257 + 13));
            eventMousePoint.Add("Wind Restored", new Point(423 + 13, 365 + 13));
            eventMousePoint.Add("Clean Water", new Point(220 + 13, 360 + 13));

            shardsMouseLocation.Add("Water Vein 1", new Point(261 + 13, 305 + 13));
            shardsMouseLocation.Add("Water Vein 2", new Point(280 + 13, 280 + 13));
            shardsMouseLocation.Add("Gumon Seal 1", new Point(310 + 13, 267 + 13));
            shardsMouseLocation.Add("Gumon Seal 2", new Point(345 + 13, 267 + 13));
            shardsMouseLocation.Add("Sunstone 2", new Point(376 + 13, 280 + 13));
            shardsMouseLocation.Add("Sunstone 1", new Point(398 + 13, 305 + 13));
        }

        #endregion

        #region EventHandlers
        private void Tracker_Load(object sender, EventArgs e)
        {
            // Start background update loop when the tracker is loaded
            // Avoid modified collection exception of dictionaries conflicted between init and update loop
            if (auto_update)
            {
                this.TurnOnAutoUpdate();
            }
        }

        private void Tracker_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && draggable)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }
        protected void CloseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
        protected void AutoUpdateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            auto_update = !auto_update;

            if (auto_update)
            {
                TurnOnAutoUpdate();
            }
            else
            {
                TurnOffAutoUpdate();
                SetRelicDefaults();
                Refresh();
            }
        }
        private void AlwaysOnTopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.TopMost = AlwaysOnTopToolStripMenuItem.Checked;
        }
        protected void MoveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            draggable = !draggable;
        }
        protected void Tracker_MouseClick(object sender, MouseEventArgs e)
        {
            int x, y;

            x = e.X;
            y = e.Y;

            if (ToggleMouseClick(x, y))
            {
                bool tmp_auto_update = auto_update;
                //try turning off auto update for a moment
                if (tmp_auto_update)
                {
                    this.TurnOffAutoUpdate();
                    trackerBitfields = MemoryBitfields.Empty;
                }

                this.Refresh();
                if (tmp_auto_update)
                {
                    this.TurnOnAutoUpdate();
                }
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            UpdateGraphics(e.Graphics);
        }
        private void SettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            settings.Show();
        }
        private void ClearToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ClearAll();
        }
        private void Tracker_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Stop background update loop before closing
            // Avoid an update on disposed objects
            if (auto_update)
            {
                this.TurnOffAutoUpdate();
            }

            TrackerSettings.Default.MapFontColor = (MapstoneFont.Brush as SolidBrush).Color;
            TrackerSettings.Default.MapFontFamilyName = MapstoneFont.Font.FontFamily.Name;
            TrackerSettings.Default.Background = BackColor;
            TrackerSettings.Default.RefreshRate = refresh_rate;
            TrackerSettings.Default.Opacity = Opacity;

            TrackerSettings.Default.Shards = track_shards;
            TrackerSettings.Default.Teleporters = track_teleporters;
            TrackerSettings.Default.Trees = track_trees;
            TrackerSettings.Default.Relics = track_relics;
            TrackerSettings.Default.Mapstones = track_mapstones;

            TrackerSettings.Default.DisplayEmptyRelics = display_empty_relics;
            TrackerSettings.Default.DisplayEmptyTrees = display_empty_trees;
            TrackerSettings.Default.DisplayEmptyTeleporters = display_empty_teleporters;
            TrackerSettings.Default.DisplayEmptyShards = display_empty_shards;

            TrackerSettings.Default.Pixels = tracker_size;
            TrackerSettings.Default.AlwaysOnTop = this.TopMost;
            TrackerSettings.Default.Draggable = draggable;
            TrackerSettings.Default.AutoUpdate = auto_update;

            TrackerSettings.Default.Save();

            Mem?.Dispose();
        }
        #endregion

        #region Graphics
        protected int Square(int a)
        {
            return a * a;
        }
        protected bool ToggleMouseClick(int x, int y)
        {
            double mouse_scaling = ((image_pixel_size * 1.0) / PIXEL_DEF);
            int CUR_TOL = (int)(TOL * mouse_scaling);

            if (track_mapstones && (Math.Sqrt(Square(x - (int)(mapstoneMousePoint.X * mouse_scaling)) + Square(y - (int)(mapstoneMousePoint.Y * mouse_scaling))) <= 2 * CUR_TOL))
            {
                mapstone_count += 1;
                if (mapstone_count > 9)
                {
                    mapstone_count = 0;
                }
                return true;
            }

            foreach (KeyValuePair<String, Point> sk in skillMousePoint)
            {
                if (Math.Sqrt(Square(x - (int)(sk.Value.X * mouse_scaling)) + Square(y - (int)(sk.Value.Y * mouse_scaling))) <= 2 * CUR_TOL)
                {
                    if (haveSkill.ContainsKey(sk.Key))
                    {
                        haveSkill[sk.Key] = !haveSkill[sk.Key];
                        return true;
                    }
                }
            }

            foreach (KeyValuePair<String, Point> sk in eventMousePoint)
            {
                if (Math.Sqrt(Square(x - (int)(sk.Value.X * mouse_scaling)) + Square(y - (int)(sk.Value.Y * mouse_scaling))) <= CUR_TOL + 10)
                {
                    if (haveEvent.ContainsKey(sk.Key))
                    {
                        switch (sk.Key)
                        {
                            case "Water Vein":
                            case "Gumon Seal":
                            case "Sunstone":
                                if (track_shards)
                                {
                                    if (haveEvent[sk.Key])
                                    {
                                        haveShards[sk.Key + " 1"] = false;
                                        haveShards[sk.Key + " 2"] = false;
                                        haveEvent[sk.Key] = false;
                                    }
                                    else if (haveShards[sk.Key + " 2"])
                                    {
                                        haveShards[sk.Key + " 1"] = true;
                                        haveShards[sk.Key + " 2"] = true;
                                        haveEvent[sk.Key] = true;
                                    }
                                    else if (haveShards[sk.Key + " 1"])
                                    {
                                        haveShards[sk.Key + " 1"] = true;
                                        haveShards[sk.Key + " 2"] = true;
                                        haveEvent[sk.Key] = false;
                                    }
                                    else
                                    {
                                        haveShards[sk.Key + " 1"] = true;
                                        haveShards[sk.Key + " 2"] = false;
                                        haveEvent[sk.Key] = false;
                                    }
                                }
                                else
                                {
                                    haveEvent[sk.Key] = !haveEvent[sk.Key];
                                }
                                break;
                            case "Warmth Returned":
                            case "Wind Restored":
                            case "Clean Water":
                                haveEvent[sk.Key] = !haveEvent[sk.Key];
                                break;
                        }
                        return true;
                    }
                }
            }

            if (track_trees)
            {
                foreach (KeyValuePair<String, Point> sk in treeMouseLocation)
                {
                    if (Math.Sqrt(Square(x - (int)(sk.Value.X * mouse_scaling)) + Square(y - (int)(sk.Value.Y * mouse_scaling))) <= CUR_TOL)
                    {
                        if (haveTree.ContainsKey(sk.Key))
                        {
                            haveTree[sk.Key] = !haveTree[sk.Key];
                            return true;
                        }
                    }
                }
            }

            if (track_teleporters)
            {
                foreach (KeyValuePair<String, Point> sk in teleporterMouseLocation)
                {
                    if (Math.Sqrt(Square(x - (int)(sk.Value.X * mouse_scaling)) + Square(y - (int)(sk.Value.Y * mouse_scaling))) <= CUR_TOL)
                    {
                        if (haveTeleporters.ContainsKey(sk.Key))
                        {
                            haveTeleporters[sk.Key] = !haveTeleporters[sk.Key];
                            return true;
                        }
                    }
                }
            }

            if (track_relics)
            {
                foreach (KeyValuePair<String, Point> sk in relicMouseLocation)
                {
                    if (Math.Sqrt(Square(x - (int)(sk.Value.X * mouse_scaling)) + Square(y - (int)(sk.Value.Y * mouse_scaling))) <= 2 * CUR_TOL)
                    {
                        if (relicExists.ContainsKey(sk.Key) && relicFound.ContainsKey(sk.Key))
                        {
                            relicExists[sk.Key] = !relicExists[sk.Key];
                            relicFound[sk.Key] = !relicFound[sk.Key];
                            return true;
                        }
                    }
                }
            }

            if (track_shards)
            {
                foreach (KeyValuePair<String, Point> sk in shardsMouseLocation)
                {
                    if (Math.Sqrt(Square(x - (int)(sk.Value.X * mouse_scaling)) + Square(y - (int)(sk.Value.Y * mouse_scaling))) <= CUR_TOL / 2)
                    {
                        if (haveShards.ContainsKey(sk.Key))
                        {
                            haveShards[sk.Key] = !haveShards[sk.Key];
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        protected void UpdateGraphics(Graphics g)
        {
            try
            {
                /*
                 * Drawing consists of the following steps:
                 * (1) Drawing the Skills (either grayed out or colored in)
                 * (2) Drawing the Trees
                 * (3) Drawing the Events
                 * (4) Drawing the Shards
                 * (5) Drawing the Teleporters
                 * (6) Drawing the Relics
                 * (7) Drawing the Mapstone
                 * (8) Putting the skill wheel on top
                 * */

                #region Draw
                #region Skills

                g.DrawImage(imageGSkills, ClientRectangle);
                foreach (KeyValuePair<String, bool> sk in haveSkill)
                {
                    if (sk.Value)
                    {
                        g.DrawImage(skillImages[sk.Key], ClientRectangle);
                    }
                }
                #endregion

                #region Trees
                /* Trees are drawn if:
                 * (a) track_trees is on
                 * (b) auto_update and mode_force_trees are on
                 * (*) only draw grey trees if display_empty_trees is on
                 */
                if (track_trees || (auto_update && mode_force_trees))
                {
                    if (display_empty_trees)
                    {
                        g.DrawImage(imageGTrees, ClientRectangle);
                    }

                    foreach (KeyValuePair<String, bool> sk in haveTree)
                    {
                        if (sk.Value)
                        {
                            g.DrawImage(treeImages[sk.Key], ClientRectangle);
                        }
                    }
                }
                #endregion

                #region Events
                foreach (KeyValuePair<String, bool> ev in haveEvent)
                {
                    if (ev.Value)
                    {
                        g.DrawImage(eventImages[ev.Key], ClientRectangle);
                    }
                    else
                    {
                        g.DrawImage(eventGreyImages[ev.Key], ClientRectangle);
                    }
                }
                #endregion

                #region Shards
                /* Shards are drawn if
                 * (a) auto_update is off and track_shards is on (manual only)
                 * (b) auto_update and mode_shards are on
                 * (*) only draw grey shards if display_empty_shards is on
                 */
                if ((!auto_update && track_shards) || (auto_update && mode_shards))
                {
                    if (display_empty_shards)
                    {
                        g.DrawImage(imageGShards, ClientRectangle);
                    }

                    foreach (KeyValuePair<String, bool> ev in haveShards)
                    {
                        if (ev.Value)
                        {
                            g.DrawImage(shardImages[ev.Key], ClientRectangle);
                        }
                    }
                }
                #endregion

                #region Teleporters
                /* Teleporters are drawn if:
                 * (a) track_teleporters is on
                 * (*) only drawn grey teleporters if display_empty_teleporters is on
                 */
                if (track_teleporters)
                {
                    if (display_empty_teleporters)
                    {
                        g.DrawImage(imageGTeleporters, ClientRectangle);
                    }

                    foreach (KeyValuePair<String, bool> tp in haveTeleporters)
                    {
                        if (tp.Value)
                        {
                            g.DrawImage(teleporterImages[tp.Key], ClientRectangle);
                        }
                    }
                }
                #endregion

                #region Relic
                /* Relics are drawn if:
                 * (a) track_relics is on
                 * (b) auto_update and world tour are on
                 * (*) only drawn grey relics if display_empty_relics is on
                 */
                if (track_relics || (auto_update && mode_world_tour))
                {
                    if (display_empty_relics)
                    {
                        foreach (KeyValuePair<String, bool> relic in relicExists)
                        {
                            if (relic.Value)
                            {
                                g.DrawImage(relicExistImages[relic.Key], ClientRectangle);
                            }
                        }
                    }

                    foreach (KeyValuePair<String, bool> relic in relicFound)
                    {
                        if (relic.Value)
                        {
                            g.DrawImage(relicFoundImages[relic.Key], ClientRectangle);
                        }
                    }
                }
                #endregion

                #region Mapstone
                /* Mapstone count is drawn if:
                 * (a) track_mapstones is on
                 * (b) auto_update and mode_force_maps are on
                 */
                if (track_mapstones || (auto_update && mode_force_maps))
                {
                    g.DrawImage(imageMapStone, ClientRectangle);
                    g.DrawString(mapstone_count.ToString() + "/9", mapstoneFont.Font, mapstoneFont.Brush, mapstoneFont.Location.X, mapstoneFont.Location.Y);
                }
                #endregion

                g.DrawImage(imageSkillWheelDouble, ClientRectangle);

#if DEBUG
                // Disable by default only used for debug purpose
                // DrawMouseLocation(g);
#endif
                #endregion
            }
            catch
            {

            }
        }

        private void DrawMouseLocation(Graphics g)
        {
            DrawMouseLocation(g, mapstoneMousePoint, Brushes.LightCoral);
            DrawMouseLocation(g, skillMousePoint.Values, Brushes.Red);
            DrawMouseLocation(g, treeMouseLocation.Values, Brushes.Yellow);
            DrawMouseLocation(g, eventMousePoint.Values, Brushes.Cyan);
            DrawMouseLocation(g, teleporterMouseLocation.Values, Brushes.Magenta);
            DrawMouseLocation(g, relicMouseLocation.Values, Brushes.LightSkyBlue);
            DrawMouseLocation(g, shardsMouseLocation.Values, Brushes.Pink);
            DrawMouseLocation(g, skillMousePoint.Values, Brushes.Red);
        }

        private void DrawMouseLocation(Graphics g, ICollection<Point> locations, Brush brush)
        {
            foreach (var location in locations)
            {
                DrawMouseLocation(g, location, brush);
            }
        }

        private void DrawMouseLocation(Graphics g, Point location, Brush brush)
        {
            g.FillRectangle(brush, location.X, location.Y, 1, 1);
        }

        protected void ClearAll()
        {
            bool tmp_auto_update = this.auto_update;
            if (tmp_auto_update)
            {
                this.TurnOffAutoUpdate();
                trackerBitfields = MemoryBitfields.Empty;
            }

            for (int i = 0; i < haveSkill.Count; i++)
            {
                haveSkill[haveSkill.ElementAt(i).Key] = false;
            }
            for (int i = 0; i < haveTree.Count; i++)
            {
                haveTree[haveTree.ElementAt(i).Key] = false;
            }
            for (int i = 0; i < haveEvent.Count; i++)
            {
                haveEvent[haveEvent.ElementAt(i).Key] = false;
            }
            for (int i = 0; i < haveShards.Count; i++)
            {
                haveShards[haveShards.ElementAt(i).Key] = false;
            }
            for (int i = 0; i < haveTeleporters.Count; i++)
            {
                haveTeleporters[haveTeleporters.ElementAt(i).Key] = false;
            }
            for (int i = 0; i < relicFound.Count; i++)
            {
                relicFound[relicFound.ElementAt(i).Key] = false;
                relicExists[relicExists.ElementAt(i).Key] = true;
            }
            mapstone_count = 0;

            Refresh();

            if (tmp_auto_update)
            {
                this.TurnOnAutoUpdate();
            }
        }
        protected void SoftReset()
        {
            ClearAll();

            this.settings.Visible = false;
            this.settings.Reset();

            SetDefaults();
            if (auto_update && !TrackerSettings.Default.AutoUpdate)
            {
                TurnOffAutoUpdate();
            }
            auto_update = TrackerSettings.Default.AutoUpdate;
            this.AutoUpdateToolStripMenuItem.Checked = TrackerSettings.Default.AutoUpdate;

            draggable = TrackerSettings.Default.Draggable;
            this.MoveToolStripMenuItem.Checked = TrackerSettings.Default.Draggable;
        }
        #endregion

        #region AutoUpdate
        bool paused;
        bool started;
        protected void TurnOnAutoUpdate()
        {
            if (started && paused)
            {
                th.Resume();
                started = true;
                paused = false;
            }
            else if (!(started))
            {
                th.Start();
                started = true;
                paused = false;
            }
        }
        protected void TurnOffAutoUpdate()
        {
            if (!paused && started)
            {
                th.Suspend();
                started = true;
                paused = true;
            }
        }

        private void UpdateLoop()
        {
            bool lastHooked = false;
            while (true)
            {
                try
                {
                    bool hooked = Mem.HookProcess();
                    if (hooked)
                    {
                        UpdateValues();
                    }
                    if (lastHooked != hooked)
                    {
                        lastHooked = hooked;
                        this.Invoke((Action)delegate () { LabelBlank.Visible = false; });
                    }
                    Thread.Sleep((int)refresh_time);
                }
                catch (Exception exc)
                {
                    if (MessageBox.Show(exc.StackTrace.ToString() + "\nWould you like to abort and soft reset the tracker?", "Exception Occured", MessageBoxButtons.AbortRetryIgnore) == DialogResult.Abort)
                        SoftReset();
                }
            }
        }
        private void UpdateValues()
        {
            try
            {
                var bitFields = Mem.GetBitfields();
                if (bitFields.TreeBitfield != trackerBitfields.TreeBitfield
                    || bitFields.MapstoneBitfield != trackerBitfields.MapstoneBitfield
                    || bitFields.TeleporterBitfield != trackerBitfields.TeleporterBitfield
                    || bitFields.RelicBitfield != trackerBitfields.RelicBitfield                    
                    || bitFields.KeyEventBitfield != trackerBitfields.KeyEventBitfield)
                {
                    trackerBitfields = bitFields;

                    UpdateSkills();
                    UpdateTrees();
                    UpdateEvents();
                    UpdateRelics();
                    UpdateTeleporters();
                    UpdateWarmthFrags();
                    UpdateMapstoneProgression();

                    //the following works but is "incorrect"
                    if (this.InvokeRequired)
                    {
                        this.Invoke(new MethodInvoker(delegate { this.Refresh(); }));
                    }
                    else
                    {
                        this.Refresh();
                    }                        
                }
            }
            catch (Exception exc)
            {
                if (MessageBox.Show(exc.StackTrace.ToString() + "\nWould you like to abort and soft reset the tracker?", "Exception Occured", MessageBoxButtons.AbortRetryIgnore) == DialogResult.Abort)
                    SoftReset();
            }
        }

        private void UpdateSkills()
        {
            foreach (KeyValuePair<string, int> skill in skillBits)
            {
                haveSkill[skill.Key] = Mem.GetBit(trackerBitfields.TreeBitfield, skill.Value);
            }
        }
        private void UpdateTrees()
        {
            foreach (KeyValuePair<string, int> tree in treeBits)
            {
                haveTree[tree.Key] = Mem.GetBit(trackerBitfields.TreeBitfield, tree.Value);
            }
        }
        private void UpdateEvents()
        {
            int bf = trackerBitfields.KeyEventBitfield;
            haveShards["Water Vein 1"] = Mem.GetBit(bf, 0);
            haveShards["Water Vein 2"] = Mem.GetBit(bf, 1);
            haveShards["Gumon Seal 1"] = Mem.GetBit(bf, 3);
            haveShards["Gumon Seal 2"] = Mem.GetBit(bf, 4);
            haveShards["Sunstone 1"] = Mem.GetBit(bf, 6);
            haveShards["Sunstone 2"] = Mem.GetBit(bf, 7);
            haveEvent["Water Vein"] = Mem.GetBit(bf, 2);
            haveEvent["Gumon Seal"] = Mem.GetBit(bf, 5);
            haveEvent["Sunstone"] = Mem.GetBit(bf, 8);
            haveEvent["Clean Water"] = Mem.GetBit(bf, 9);
            haveEvent["Wind Restored"] = Mem.GetBit(bf, 10);
            mode_force_trees = Mem.GetBit(bf, 11);
            mode_shards = Mem.GetBit(bf, 12);
            mode_warmth_fragments = Mem.GetBit(bf, 13);
            mode_world_tour = Mem.GetBit(bf, 14);
        }
        private void UpdateTeleporters()
        {
            foreach (KeyValuePair<string, int> tp in teleporterBits)
            {
                haveTeleporters[tp.Key] = Mem.GetBit(trackerBitfields.TeleporterBitfield, tp.Value);
            }
        }
        private void UpdateRelics()
        {
            int bf = 0;
            if (mode_world_tour)
            {
                bf = trackerBitfields.RelicBitfield;
            }

            foreach (KeyValuePair<string, int> relic in relicExistsBits)
            {
                relicExists[relic.Key] = Mem.GetBit(bf, relic.Value);
            }
            foreach (KeyValuePair<string, int> relic in relicFoundBits)
            {
                relicFound[relic.Key] = Mem.GetBit(bf, relic.Value);
            }
        }
        private void UpdateWarmthFrags()
        {
            if (!mode_warmth_fragments)
            {
                return;
            }

            current_frags = trackerBitfields.MapstoneBitfield >> 9;
            max_frags = trackerBitfields.TeleporterBitfield >> 10;
        }
        private void UpdateMapstoneProgression()
        {
            int ms = 0;
            foreach (int bit in mapstoneBits.Values)
            {
                if (Mem.GetBit(trackerBitfields.MapstoneBitfield, bit))
                {
                    ms++;
                }
            }
            mapstone_count = ms;
        }
        #endregion

        internal void UpdateTrackerSize(TrackerPixelSizes trackerSize)
        {
            TrackerSize = trackerSize;
            UpdateImages();
            Size = new Size(image_pixel_size, image_pixel_size);
        }
    }
}
