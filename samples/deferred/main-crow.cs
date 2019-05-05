﻿using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Glfw;
using VK;
using CVKL;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace deferred {
	class Program : Crow.CrowWin {
		static void Main (string[] args) {
			using (Program vke = new Program ()) {
				vke.Run ();
			}
		}

		public Crow.Command CMDViewScenes, CMDViewEditor, CMDViewDebug, CMDViewMaterials;
		void init_crow_commands () {
			CMDViewScenes = new Crow.Command (new Action (() => loadWindow ("#deferred.main.crow", this))) { Caption = "Lighting", Icon = new Crow.SvgPicture ("#deferred.crow.svg"), CanExecute = true };
			CMDViewEditor = new Crow.Command (new Action (() => loadWindow ("#deferred.scenes.crow", this))) { Caption = "Scenes", Icon = new Crow.SvgPicture ("#deferred.crow.svg"), CanExecute = true };
			CMDViewDebug = new Crow.Command (new Action (() => loadWindow ("#deferred.debug.crow", this))) { Caption = "Debug", Icon = new Crow.SvgPicture ("#deferred.crow.svg"), CanExecute = true };
			CMDViewMaterials = new Crow.Command (new Action (() => loadWindow ("#deferred.materials.crow", this))) { Caption = "Materials", Icon = new Crow.SvgPicture ("#deferred.crow.svg"), CanExecute = true };
		}

		void onApplyMaterialChanges (object sender, Crow.MouseButtonEventArgs e) {
			Crow.ListBox lb = ((sender as Crow.Widget).Parent as Crow.Group).Children[0] as Crow.ListBox;
			renderer.model.materialUBO.Update (renderer.model.materials);
		}

		public DeferredPbrRenderer.DebugView CurrentDebugView {
			get { return renderer.currentDebugView; }
			set {
				if (value == renderer.currentDebugView)
					return;
				lock(crow.UpdateMutex)
					renderer.currentDebugView = value;
				rebuildBuffers = true;
				NotifyValueChanged ("CurrentDebugView", renderer.currentDebugView);
			}
		}

		public float Gamma {
			get { return renderer.matrices.gamma; }
			set {
				if (value == renderer.matrices.gamma)
					return;
				renderer.matrices.gamma = value;
				NotifyValueChanged ("Gamma", value);
				updateViewRequested = true;
			}
		}
		public float Exposure {
			get { return renderer.matrices.exposure; }
			set {
				if (value == renderer.matrices.exposure)
					return;
				renderer.matrices.exposure = value;
				NotifyValueChanged ("Exposure", value);
				updateViewRequested = true;
			}
		}
		public float LightStrength {
			get { return renderer.lights[renderer.lightNumDebug].color.X; }
			set {
				if (value == renderer.lights[renderer.lightNumDebug].color.X)
					return;
				renderer.lights[renderer.lightNumDebug].color = new Vector4(value);
				NotifyValueChanged ("LightStrength", value);
				renderer.uboLights.Update (renderer.lights);
			}
		}
		public List<DeferredPbrRenderer.Light> Lights => renderer.lights.ToList ();
		public List<Model.Scene> Scenes => renderer.model.Scenes;
		public PbrModel.PbrMaterial[] Materials => renderer.model.materials;


		protected override void configureEnabledFeatures (VkPhysicalDeviceFeatures available_features, ref VkPhysicalDeviceFeatures features) {
			base.configureEnabledFeatures (available_features, ref features);

			features.samplerAnisotropy = available_features.samplerAnisotropy;
			features.sampleRateShading = available_features.sampleRateShading;
			features.geometryShader = available_features.geometryShader;

			if (available_features.textureCompressionBC) { 
			}
		}

		protected override void createQueues () {
			base.createQueues ();
			transferQ = new Queue (dev, VkQueueFlags.Transfer);
		}

		string[] cubemapPathes = {
			"../../../samples/data/textures/papermill.ktx",
			"../../../samples/data/textures/cubemap_yokohama_bc3_unorm.ktx",
			"../../../samples/data/textures/gcanyon_cube.ktx",
			"../../../samples/data/textures/pisa_cube.ktx",
			"../../../samples/data/textures/uffizi_cube.ktx",
		};
		string[] modelPathes = {
				"../../../samples/data/models/DamagedHelmet/glTF/DamagedHelmet.gltf",
				"../../../samples/data/models/shadow.glb",
				"../../../samples/data/models/Hubble.glb",
				"../../../samples/data/models/MER_static.glb",
				"../../../samples/data/models/ISS_stationary.glb",
			};

		int curModelIndex = 0;
		bool reloadModel;

		Queue transferQ;
		DeferredPbrRenderer renderer;

		Program () : base(true) {
			camera = new Camera (Utils.DegreesToRadians (45f), 1f, 0.1f, 16f);
			camera.SetPosition (0, 0, 2);

			renderer = new DeferredPbrRenderer (dev, swapChain, presentQueue, cubemapPathes[2], camera.NearPlane, camera.FarPlane);
			renderer.LoadModel (transferQ, modelPathes[curModelIndex]);
			camera.Model = Matrix4x4.CreateScale (1f / Math.Max (Math.Max (renderer.modelAABB.Width, renderer.modelAABB.Height), renderer.modelAABB.Depth));

			init_crow_commands ();

			crow.Load ("#deferred.menu.crow").DataSource = this;
			//crow.Load ("#deferred.testImage.crow").DataSource = this;

			//crow.LoadIMLFragment (@"<Image Width='32' Height='32' Margin='2' HorizontalAlignment='Left' VerticalAlignment='Top' Path='/mnt/devel/gts/vkvg/build/data/tiger.svg' Background='Grey' MouseEnter='{Background=Blue}' MouseLeave='{Background=White}'/>");
		}

		protected override void recordDraw (CommandBuffer cmd, int imageIndex) {
			renderer.buildCommandBuffers (cmd, imageIndex);
		}

		public override void UpdateView () {
			renderer.UpdateView (camera);
			updateViewRequested = false;
#if WITH_SHADOWS
			if (renderer.shadowMapRenderer.updateShadowMap)
				renderer.shadowMapRenderer.update_shadow_map (cmdPool);
#endif
		}

		int frameCount = 0;
		public override void Update () {
			if (reloadModel) {
				renderer.LoadModel (transferQ, modelPathes[curModelIndex]);
				reloadModel = false;
				camera.Model = Matrix4x4.CreateScale (1f / Math.Max (Math.Max (renderer.modelAABB.Width, renderer.modelAABB.Height), renderer.modelAABB.Depth));
				updateViewRequested = true;
				rebuildBuffers = true;
#if WITH_SHADOWS
				renderer.shadowMapRenderer.updateShadowMap = true;
#endif
			}

			base.Update ();

			if (++frameCount > 20) {
				NotifyValueChanged ("fps", fps);
				frameCount = 0;
			}
		}
		protected override void OnResize () {		
			renderer.Resize ();
			base.OnResize ();
		}

		#region Mouse and keyboard

		protected override void onMouseMove (double xPos, double yPos) {
			if (crow.ProcessMouseMove ((int)xPos, (int)yPos))
				return;

			double diffX = lastMouseX - xPos;
			double diffY = lastMouseY - yPos;
			if (MouseButton[0]) {
				camera.Rotate ((float)-diffX, (float)-diffY);
			} else if (MouseButton[1]) {
				camera.SetZoom ((float)diffY);
			} else
				return;

			updateViewRequested = true;
		}
		protected override void onMouseButtonDown (Glfw.MouseButton button) {
			if (crow.ProcessMouseButtonDown ((Crow.MouseButton)button))
				return;
			base.onMouseButtonDown (button);
		}
		protected override void onMouseButtonUp (Glfw.MouseButton button) {
			if (crow.ProcessMouseButtonUp ((Crow.MouseButton)button))
				return;
			base.onMouseButtonUp (button);
		}
		protected override void onKeyDown (Key key, int scanCode, Modifier modifiers) {
			if (crow.ProcessKeyDown ((Crow.Key)key))
				return;
			switch (key) {
				case Key.F:
					if (modifiers.HasFlag (Modifier.Shift)) {
						renderer.debugFace--;
						if (renderer.debugFace < 0)
							renderer.debugFace = 5;
					} else {
						renderer.debugFace++;
						if (renderer.debugFace >= 5)
							renderer.debugFace = 0;
					}
					rebuildBuffers = true;
					break;
				case Key.M:
					if (modifiers.HasFlag (Modifier.Shift)) {
						renderer.debugMip--;
						if (renderer.debugMip < 0)
							renderer.debugMip = (int)renderer.envCube.prefilterCube.CreateInfo.mipLevels - 1;
					} else {
						renderer.debugMip++;
						if (renderer.debugMip >= renderer.envCube.prefilterCube.CreateInfo.mipLevels)
							renderer.debugMip = 0;
					}
					rebuildBuffers = true;
					break;
				case Key.L:
					if (modifiers.HasFlag (Modifier.Shift)) {
						renderer.lightNumDebug--;
						if (renderer.lightNumDebug < 0)
							renderer.lightNumDebug = (int)renderer.lights.Length - 1;
					} else {
						renderer.lightNumDebug++;
						if (renderer.lightNumDebug >= renderer.lights.Length)
							renderer.lightNumDebug = 0;
					}
					rebuildBuffers = true;
					break;
				case Key.Keypad0:
				case Key.Keypad1:
				case Key.Keypad2:
				case Key.Keypad3:
				case Key.Keypad4:
				case Key.Keypad5:
				case Key.Keypad6:
				case Key.Keypad7:
				case Key.Keypad8:
				case Key.Keypad9:
					renderer.currentDebugView = (DeferredPbrRenderer.DebugView)(int)key-320;
					rebuildBuffers = true;
					break;
				case Key.KeypadDivide:
					renderer.currentDebugView = DeferredPbrRenderer.DebugView.irradiance;
					rebuildBuffers = true;
					break;
				case Key.S:
					if (modifiers.HasFlag (Modifier.Control)) {
						renderer.pipelineCache.Save ();
						Console.WriteLine ($"Pipeline Cache saved.");
					} else {
						renderer.currentDebugView = DeferredPbrRenderer.DebugView.shadowMap;
						rebuildBuffers = true; 
					}
					break;
				case Key.Up:
					if (modifiers.HasFlag (Modifier.Shift))
						renderer.MoveLight(-Vector4.UnitZ);
					else
						camera.Move (0, 0, 1);
					break;
				case Key.Down:
					if (modifiers.HasFlag (Modifier.Shift))
						renderer.MoveLight (Vector4.UnitZ);
					else
						camera.Move (0, 0, -1);
					break;
				case Key.Left:
					if (modifiers.HasFlag (Modifier.Shift))
						renderer.MoveLight (-Vector4.UnitX);
					else
						camera.Move (1, 0, 0);
					break;
				case Key.Right:
					if (modifiers.HasFlag (Modifier.Shift))
						renderer.MoveLight (Vector4.UnitX);
					else
						camera.Move (-1, 0, 0);
					break;
				case Key.PageUp:
					if (modifiers.HasFlag (Modifier.Shift))
						renderer.MoveLight (Vector4.UnitY);
					else
						camera.Move (0, 1, 0);
					break;
				case Key.PageDown:
					if (modifiers.HasFlag (Modifier.Shift))
						renderer.MoveLight (-Vector4.UnitY);
					else
						camera.Move (0, -1, 0);
					break;
				case Key.F2:
					if (modifiers.HasFlag (Modifier.Shift))
						renderer.matrices.exposure -= 0.3f;
					else
						renderer.matrices.exposure += 0.3f;
					break;
				case Key.F3:
					if (modifiers.HasFlag (Modifier.Shift))
						renderer.matrices.gamma -= 0.1f;
					else
						renderer.matrices.gamma += 0.1f;
					break;
				case Key.F4:
					if (camera.Type == Camera.CamType.FirstPerson)
						camera.Type = Camera.CamType.LookAt;
					else
						camera.Type = Camera.CamType.FirstPerson;
					Console.WriteLine ($"camera type = {camera.Type}");
					break;
				case Key.KeypadAdd:
					curModelIndex++;
					if (curModelIndex >= modelPathes.Length)
						curModelIndex = 0;
					reloadModel = true;
					break;
				case Key.KeypadSubtract:
					curModelIndex--;
					if (curModelIndex < 0)
						curModelIndex = modelPathes.Length -1;
					reloadModel = true;
					break;
				default:
					base.onKeyDown (key, scanCode, modifiers);
					return;
			}
			updateViewRequested = true;
		}
		protected override void onKeyUp (Key key, int scanCode, Modifier modifiers) {
			if (crow.ProcessKeyUp ((Crow.Key)key))
				return;
		}
		protected override void onChar (CodePoint cp) {
			if (crow.ProcessKeyPress (cp.ToChar ()))
				return;
		}
		#endregion

		protected override void Dispose (bool disposing) {
			if (disposing) {
				if (!isDisposed) 
					renderer.Dispose ();
			}

			base.Dispose (disposing);
		}
	}
}
