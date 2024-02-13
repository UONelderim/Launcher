﻿using ImGuiNET;
 using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Num = System.Numerics;

namespace Nelderim.Launcher
{
    public class NelderimLauncher : Game
    {
        private GraphicsDeviceManager _graphics;
        private ImGuiRenderer _imGuiRenderer;

        private Texture2D _xnaTexture;
        private IntPtr _imGuiTexture;

        public NelderimLauncher()
        {
            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreferredBackBufferWidth = 600;
            _graphics.PreferredBackBufferHeight = 300;
            _graphics.PreferMultiSampling = true;
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            _imGuiRenderer = new ImGuiRenderer(this);
            _imGuiRenderer.RebuildFontAtlas();
            base.Initialize();
        }

        protected override void LoadContent()
        {
			_xnaTexture = CreateTexture(GraphicsDevice, 300, 150, pixel =>
			{
				var red = pixel % 300 / 2;
				return new Color(red, 1, 1);
			});
			_imGuiTexture = _imGuiRenderer.BindTexture(_xnaTexture);
            base.LoadContent();
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            _imGuiRenderer.BeforeLayout(gameTime);
            ImGuiLayout();
            _imGuiRenderer.AfterLayout();

            base.Draw(gameTime);
        }

        private bool show_test_window = false;

        private static bool useWorkArea = true;

        private static int flags = (int)(ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove |
                                                         ImGuiWindowFlags.NoSavedSettings);

        protected virtual void ImGuiLayout()
        {
	        var viewport = ImGui.GetMainViewport();
	        ImGui.SetNextWindowPos(useWorkArea ? viewport.WorkPos : viewport.Pos);
	        ImGui.SetNextWindowSize(useWorkArea ? viewport.WorkSize : viewport.Size);

	        if (ImGui.Begin("Example: Fullscreen window", (ImGuiWindowFlags)flags))
	        {
		        ImGui.Checkbox("Use work area instead of main area", ref useWorkArea);
		        ImGui.SameLine();
		        ImGui.Text("Main Area = entire viewport,\nWork Area = entire viewport minus sections used by the main menu bars, task bars etc.\n\nEnable the main-menu bar in Examples menu to see the difference.");

		        ImGui.CheckboxFlags("ImGuiWindowFlags_NoBackground", ref flags, (int)ImGuiWindowFlags.NoBackground);
		        ImGui.CheckboxFlags("ImGuiWindowFlags_NoDecoration", ref flags, (int)ImGuiWindowFlags.NoDecoration);
		        ImGui.Indent();
		        ImGui.CheckboxFlags("ImGuiWindowFlags_NoTitleBar", ref flags, (int)ImGuiWindowFlags.NoTitleBar);
		        ImGui.CheckboxFlags("ImGuiWindowFlags_NoCollapse", ref flags, (int)ImGuiWindowFlags.NoCollapse);
		        ImGui.CheckboxFlags("ImGuiWindowFlags_NoScrollbar", ref flags, (int)ImGuiWindowFlags.NoScrollbar);
		        ImGui.Unindent();

		        {
			        if (ImGui.Button("Test Window")) show_test_window = !show_test_window;
		        }
		        if (show_test_window)
		        {
			        ImGui.SetNextWindowPos(new Num.Vector2(650, 20), ImGuiCond.FirstUseEver);
			        ImGui.ShowDemoWindow(ref show_test_window);
		        }
	        }
	        ImGui.End();
        }

		public static Texture2D CreateTexture(GraphicsDevice device, int width, int height, Func<int, Color> paint)
		{
			var texture = new Texture2D(device, width, height);
			Color[] data = new Color[width * height];
			for(var pixel = 0; pixel < data.Length; pixel++)
			{
				data[pixel] = paint( pixel );
			}
			texture.SetData( data );
			return texture;
		}
	}
}