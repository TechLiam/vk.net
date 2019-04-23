﻿using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Glfw;
using VK;
using CVKL;

namespace pbrSachaWillem {
	class Program : VkWindow{	
		static void Main (string[] args) {
			using (Program vke = new Program ()) {
				vke.Run ();
			}
		}

		protected override void configureEnabledFeatures (ref VkPhysicalDeviceFeatures features) {
			base.configureEnabledFeatures (ref features);
			features.pipelineStatisticsQuery = true;
			//features.samplerAnisotropy = true;
		}

		VkSampleCountFlags samples = VkSampleCountFlags.SampleCount4;

		Framebuffer[] frameBuffers;
		PBRPipeline pbrPipeline;

		#region stats
		PipelineStatisticsQueryPool statPool;
		TimestampQueryPool timestampQPool;
		ulong[] results;

		#endregion

		#region ui
		DescriptorSet dsDebugImg;
		void initDebugImg () {
			dsDebugImg = descriptorPool.Allocate (descLayoutMain);
			pbrPipeline.envCube.debugImg.Descriptor.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
			DescriptorSetWrites uboUpdate = new DescriptorSetWrites (dsDebugImg, descLayoutMain);
			uboUpdate.Write (dev, pbrPipeline.envCube.debugImg.Descriptor);
		}

		vkvg.Device vkvgDev;
        vkvg.Surface vkvgSurf;
		Image vkvgImage;

		DescriptorPool descriptorPool;
		DescriptorSetLayout descLayoutMain;
		DescriptorSet dsVkvgOverlay;
		Pipeline uiPipeline;

		void initUIPipeline () {
			descriptorPool = new DescriptorPool (dev, 2, new VkDescriptorPoolSize (VkDescriptorType.CombinedImageSampler, 2));
			descLayoutMain = new DescriptorSetLayout (dev,
				new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler));

			dsVkvgOverlay = descriptorPool.Allocate (descLayoutMain);

			GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.TriangleList, samples, false);
			cfg.RenderPass = pbrPipeline.RenderPass;
			cfg.Layout = new PipelineLayout (dev, descLayoutMain);
			cfg.AddShader (VkShaderStageFlags.Vertex, "shaders/FullScreenQuad.vert.spv");
			cfg.AddShader (VkShaderStageFlags.Fragment, "shaders/simpletexture.frag.spv");
			cfg.blendAttachments[0] = new VkPipelineColorBlendAttachmentState (true);

			uiPipeline = new GraphicPipeline (cfg);

		}
		void initUISurface () {
			vkvgImage?.Dispose ();
			vkvgSurf?.Dispose ();
			vkvgSurf = new vkvg.Surface (vkvgDev, (int)swapChain.Width, (int)swapChain.Height);
			vkvgImage = new Image (dev, new VkImage ((ulong)vkvgSurf.VkImage.ToInt64 ()), VkFormat.B8g8r8a8Unorm,
				VkImageUsageFlags.ColorAttachment, (uint)vkvgSurf.Width, (uint)vkvgSurf.Height);
			vkvgImage.CreateView (VkImageViewType.ImageView2D, VkImageAspectFlags.Color);
			vkvgImage.CreateSampler (VkFilter.Nearest, VkFilter.Nearest, VkSamplerMipmapMode.Nearest, VkSamplerAddressMode.ClampToBorder);

			vkvgImage.Descriptor.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
			DescriptorSetWrites uboUpdate = new DescriptorSetWrites (dsVkvgOverlay, descLayoutMain);
			uboUpdate.Write (dev, vkvgImage.Descriptor);
		}

		void vkvgDraw () {
            using (vkvg.Context ctx = new vkvg.Context (vkvgSurf)) {
				ctx.Operator = vkvg.Operator.Clear;
				ctx.Paint ();
				ctx.Operator = vkvg.Operator.Over;

				ctx.LineWidth = 1;
				ctx.SetSource (0.1, 0.1, 0.1, 0.8);
				ctx.Rectangle (5.5, 5.5, 320, 300);
				ctx.FillPreserve ();
				ctx.Flush ();
				ctx.SetSource (0.8, 0.8, 0.8);
				ctx.Stroke ();

				ctx.FontFace = "mono";
				ctx.FontSize = 8;
				int x = 16;
				int y = 40, dy = 16;
				ctx.MoveTo (x, y);
				ctx.ShowText (string.Format ($"fps:     {fps,5} "));
				ctx.MoveTo (x + 200, y - 0.5);
				ctx.Rectangle (x + 200, y - 8.5, 0.1 * fps, 10);
				ctx.SetSource (0.1, 0.9, 0.1);
				ctx.Fill ();
				ctx.SetSource (0.8, 0.8, 0.8);
				y += dy;
				ctx.MoveTo (x, y);
				ctx.ShowText (string.Format ($"Exposure:{pbrPipeline.parametters.exposure,5} "));
				y += dy;
				ctx.MoveTo (x, y);
				ctx.ShowText (string.Format ($"Gamma:   {pbrPipeline.parametters.gamma,5} "));
				if (results == null)
					return;

				y += dy*2;
				ctx.MoveTo (x, y);
				ctx.ShowText ("Pipeline Statistics");
				ctx.MoveTo (x-2, 4.5+y);
				ctx.LineTo (x+160, 4.5+y);
				ctx.Stroke ();
				y += 4;
				x += 20;

				for (int i = 0; i < statPool.RequestedStats.Length; i++) {
					y += dy;
					ctx.MoveTo (x, y);
					ctx.ShowText (string.Format ($"{statPool.RequestedStats[i].ToString(),-30} :{results[i],12:0,0} "));
				}
				/*y += dy;
				ctx.MoveTo (x, y);
				ctx.ShowText (string.Format ($"{"Elapsed microsecond",-20} :{timestampQPool.ElapsedMiliseconds:0.0000} "));*/
				y += dy;
				ctx.MoveTo (x, y);
				ctx.ShowText (string.Format ($"{"Debug draw:",-20} :{currentDebugView.ToString ()} "));
				y += dy;
				ctx.MoveTo (x, y);
				ctx.ShowText (string.Format ($"{"Debug Prefil Face:",-20} :{pbrPipeline.envCube.debugFace.ToString ()} "));
				y += dy;
				ctx.MoveTo (x, y);
				ctx.ShowText (string.Format ($"{"Debug Prefil Mip:",-20} :{pbrPipeline.envCube.debugMip.ToString ()} "));
			}
		}
		void recordDrawOverlay (CommandBuffer cmd) {
			uiPipeline.Bind (cmd);

			cmd.BindDescriptorSet (uiPipeline.Layout, dsVkvgOverlay);

			vkvgImage.SetLayout (cmd, VkImageAspectFlags.Color, VkImageLayout.ColorAttachmentOptimal, VkImageLayout.ShaderReadOnlyOptimal,
				VkPipelineStageFlags.ColorAttachmentOutput, VkPipelineStageFlags.FragmentShader);

			cmd.Draw (3, 1, 0, 0);

			vkvgImage.SetLayout (cmd, VkImageAspectFlags.Color, VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.ColorAttachmentOptimal,
				VkPipelineStageFlags.FragmentShader, VkPipelineStageFlags.BottomOfPipe);

			if (!showDebugImg)
				return;
			const uint debugImgSize = 256;
			const uint debugImgMargin = 10;

			cmd.BindDescriptorSet (uiPipeline.Layout, dsDebugImg);

			cmd.SetViewport (debugImgSize, debugImgSize, debugImgMargin, swapChain.Height - debugImgSize - debugImgMargin);
			cmd.SetScissor (debugImgSize, debugImgSize, (int)debugImgMargin, (int)(swapChain.Height - debugImgSize - debugImgMargin));

			cmd.Draw (3, 1, 0, 0);
		}
		#endregion

		Vector3 lightPos = new Vector3 (1, 0, 0);

		Program () {
			vkvgDev = new vkvg.Device (instance.Handle, phy.Handle, dev.VkDev.Handle, presentQueue.qFamIndex,
				vkvg.SampleCount.Sample_4, presentQueue.index);

			//UpdateFrequency = 20;
			camera.Model = Matrix4x4.CreateScale (1.0f);
			camera.SetPosition (0, 0, 3);

			pbrPipeline = new PBRPipeline(presentQueue,
				new RenderPass (dev, swapChain.ColorFormat, dev.GetSuitableDepthFormat (), samples));

			initUIPipeline ();

			statPool = new PipelineStatisticsQueryPool (dev,
				VkQueryPipelineStatisticFlags.InputAssemblyVertices |
				VkQueryPipelineStatisticFlags.InputAssemblyPrimitives |
				VkQueryPipelineStatisticFlags.ClippingInvocations |
				VkQueryPipelineStatisticFlags.ClippingPrimitives |
				VkQueryPipelineStatisticFlags.FragmentShaderInvocations);

			timestampQPool = new TimestampQueryPool (dev);
		}

		#region commands
		void buildCommandBuffers () {
			for (int i = 0; i < swapChain.ImageCount; ++i) {
				cmds[i]?.Free ();
				cmds[i] = cmdPool.AllocateCommandBuffer ();
				cmds[i].Start ();

				statPool.Begin (cmds[i]);

				recordDraw (cmds[i], frameBuffers[i]);

				statPool.End (cmds[i]);

				cmds[i].End ();
			}
		}
		void recordDraw (CommandBuffer cmd, Framebuffer fb) {
			pbrPipeline.RenderPass.Begin (cmd, fb);

			cmd.SetViewport (fb.Width, fb.Height);
			cmd.SetScissor (fb.Width, fb.Height);

			pbrPipeline.RecordDraw (cmd);

			recordDrawOverlay (cmd);

			pbrPipeline.RenderPass.End (cmd);
		}
		#endregion


		enum DebugView {
			none,
			color,
			normal,
			occlusion,
			emissive,
			metallic,
			roughness
		}

		DebugView currentDebugView = DebugView.none;
		bool queryUpdatePrefilCube, showDebugImg;

		#region update
		void updateMatrices () {
			camera.AspectRatio = (float)swapChain.Width / swapChain.Height;

			pbrPipeline.matrices.projection = camera.Projection;
			pbrPipeline.matrices.view = camera.View;
			pbrPipeline.matrices.model = camera.Model;

			BoundingBox aabb = pbrPipeline.model.DefaultScene.AABB;
			pbrPipeline.matrices.camPos = new Vector3 (
				-camera.Position.Z * (float)Math.Sin (camera.Rotation.Y) * (float)Math.Cos (camera.Rotation.X),
				 camera.Position.Z * (float)Math.Sin (camera.Rotation.X),
				 camera.Position.Z * (float)Math.Cos (camera.Rotation.Y) * (float)Math.Cos (camera.Rotation.X)
			);

			pbrPipeline.uboMats.Update (pbrPipeline.matrices, (uint)Marshal.SizeOf<PBRPipeline.Matrices> ());
			pbrPipeline.matrices.view.Translation = Vector3.Zero;
			pbrPipeline.uboSkybox.Update (pbrPipeline.matrices, (uint)Marshal.SizeOf<PBRPipeline.Matrices> ());
		}
		void updateParams () {
			pbrPipeline.parametters.debugViewInputs = (float)currentDebugView;
			pbrPipeline.uboParams.Update (pbrPipeline.parametters, (uint)Marshal.SizeOf<PBRPipeline.Params> ());
		}
		public override void UpdateView () {
			updateMatrices ();
			updateParams ();
			updateViewRequested = false;
		}
		public override void Update () {
			results = statPool.GetResults ();
			vkvgDraw ();
		}
		#endregion


		protected override void OnResize () {
			initUISurface ();

			updateMatrices ();
			updateParams ();

			if (frameBuffers != null)
				for (int i = 0; i < swapChain.ImageCount; ++i)
					frameBuffers[i]?.Dispose ();

			frameBuffers = new Framebuffer[swapChain.ImageCount];

			for (int i = 0; i < swapChain.ImageCount; ++i) {
				frameBuffers[i] = new Framebuffer (pbrPipeline.RenderPass, swapChain.Width, swapChain.Height,
					(pbrPipeline.RenderPass.Samples == VkSampleCountFlags.SampleCount1) ? new Image[] {
						swapChain.images[i],
						null
					} : new Image[] {
						null,
						null,
						swapChain.images[i]
					});
			}

			buildCommandBuffers ();
		}

		#region Mouse and keyboard
		protected override void onMouseMove (double xPos, double yPos) {
			double diffX = lastMouseX - xPos;
			double diffY = lastMouseY - yPos;
			if (MouseButton[0]) {
				camera.Rotate ((float)-diffX, (float)-diffY);
			} else if (MouseButton[1]) {
				camera.SetZoom ((float)diffY);
			}

			updateViewRequested = true;
		}

		protected override void onKeyDown (Key key, int scanCode, Modifier modifiers) {
			switch (key) {
				case Key.F:
					if (modifiers.HasFlag (Modifier.Shift)) {
						pbrPipeline.envCube.debugFace --;
						if (pbrPipeline.envCube.debugFace < 0)
							pbrPipeline.envCube.debugFace = 5;
					} else {
						pbrPipeline.envCube.debugFace ++;
						if (pbrPipeline.envCube.debugFace > 5)
							pbrPipeline.envCube.debugFace = 0;
					}
					queryUpdatePrefilCube = updateViewRequested = true;
					break;
				case Key.M:
					if (modifiers.HasFlag (Modifier.Shift)) {
						pbrPipeline.envCube.debugMip --;
						if (pbrPipeline.envCube.debugMip < 0)
							pbrPipeline.envCube.debugMip = (int)pbrPipeline.envCube.prefilterCube.CreateInfo.mipLevels - 1;
					} else {
						pbrPipeline.envCube.debugMip ++;
						if (pbrPipeline.envCube.debugMip > pbrPipeline.envCube.prefilterCube.CreateInfo.mipLevels)
							pbrPipeline.envCube.debugMip = 0;
					}
					queryUpdatePrefilCube = updateViewRequested = true;
					break;
				case Key.P:
					showDebugImg = !showDebugImg;
					queryUpdatePrefilCube = updateViewRequested = true;
					break;
				case Key.Keypad0:
					currentDebugView = DebugView.none;
					break;
				case Key.Keypad1:
					currentDebugView = DebugView.color;
					break;
				case Key.Keypad2:
					currentDebugView = DebugView.normal;
					break;
				case Key.Keypad3:
					currentDebugView = DebugView.occlusion;
					break;
				case Key.Keypad4:
					currentDebugView = DebugView.emissive;
					break;
				case Key.Keypad5:
					currentDebugView = DebugView.metallic;
					break;
				case Key.Keypad6:
					currentDebugView = DebugView.roughness;
					break;
				case Key.Up:
					if (modifiers.HasFlag (Modifier.Shift))
						lightPos -= Vector3.UnitZ;
					else
						camera.Move (0, 0, 1);
					break;
				case Key.Down:
					if (modifiers.HasFlag (Modifier.Shift))
						lightPos += Vector3.UnitZ;
					else
						camera.Move (0, 0, -1);
					break;
				case Key.Left:
					if (modifiers.HasFlag (Modifier.Shift))
						lightPos -= Vector3.UnitX;
					else
						camera.Move (1, 0, 0);
					break;
				case Key.Right:
					if (modifiers.HasFlag (Modifier.Shift))
						lightPos += Vector3.UnitX;
					else
						camera.Move (-1, 0, 0);
					break;
				case Key.PageUp:
					if (modifiers.HasFlag (Modifier.Shift))
						lightPos += Vector3.UnitY;
					else
						camera.Move (0, 1, 0);
					break;
				case Key.PageDown:
					if (modifiers.HasFlag (Modifier.Shift))
						lightPos -= Vector3.UnitY;
					else
						camera.Move (0, -1, 0);
					break;
				case Key.F1:
					if (modifiers.HasFlag (Modifier.Shift))
						pbrPipeline.parametters.exposure -= 0.3f;
					else
						pbrPipeline.parametters.exposure += 0.3f;
					break;
				case Key.F2:
					if (modifiers.HasFlag (Modifier.Shift))
						pbrPipeline.parametters.gamma -= 0.1f;
					else
						pbrPipeline.parametters.gamma += 0.1f;
					break;
				case Key.F3:
					if (camera.Type == Camera.CamType.FirstPerson)
						camera.Type = Camera.CamType.LookAt;
					else
						camera.Type = Camera.CamType.FirstPerson;
					Console.WriteLine ($"camera type = {camera.Type}");
					break;
				default:
					base.onKeyDown (key, scanCode, modifiers);
					return;
			}
			updateViewRequested = true;
		}
		#endregion

		#region dispose
		protected override void Dispose (bool disposing) {
			if (disposing) {
				if (!isDisposed) {
					dev.WaitIdle ();
					for (int i = 0; i < swapChain.ImageCount; ++i)
						frameBuffers[i]?.Dispose ();

					pbrPipeline.Dispose ();

					uiPipeline.Dispose ();
					descLayoutMain.Dispose ();
					descriptorPool.Dispose ();

					vkvgImage?.Dispose ();
					vkvgSurf?.Dispose ();
					vkvgDev.Dispose ();

					timestampQPool?.Dispose ();
					statPool?.Dispose ();
				}
			}

			base.Dispose (disposing);
		}
		#endregion
	}
}
