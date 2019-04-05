﻿using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Glfw;
using VKE;
using Vulkan;
using Buffer = VKE.Buffer;

namespace PbrSample {
	class Program : VkWindow {
		static void Main (string[] args) {
			using (Program vke = new Program ()) {
				vke.Run ();
			}
		}
			
        public struct Matrices {
			public Matrix4x4 projection;
			public Matrix4x4 view;
			public Matrix4x4 model;
			public Vector4 lightPos;
			public float gamma;
			public float exposure;
		}

		Matrices matrices = new Matrices {
			lightPos = new Vector4 (1.0f, 0.0f, 0.0f, 1.0f),
			gamma = 1.0f,
			exposure = 2.0f,
		};

		Framebuffer[] frameBuffers;

		HostBuffer uboMats;
		HostBuffer uboMatsSkybox;

		DescriptorPool descriptorPool;
		DescriptorSetLayout descLayoutMain;
		DescriptorSetLayout descLayoutTextures;

		DescriptorSet dsSkybox;
		DescriptorSet dsMain;

		Pipeline pipeline;
		Pipeline skyboxPL;

		Model model;

		GPUBuffer vboSkybox;
		Image cubemap;
		string[] cubemapPathes = {
			"../data/textures/papermill.ktx",
			"../data/textures/cubemap_yokohama_bc3_unorm.ktx",
			"../data/textures/gcanyon_cube.ktx",
			"../data/textures/pisa_cube.ktx",
			"../data/textures/uffizi_cube.ktx",
		};
		static float[] box_vertices = {
			-1.0f,-1.0f,-1.0f,    0.0f, 1.0f,  // -X side
			-1.0f,-1.0f, 1.0f,    1.0f, 1.0f,
			-1.0f, 1.0f, 1.0f,    1.0f, 0.0f,
			-1.0f, 1.0f, 1.0f,    1.0f, 0.0f,
			-1.0f, 1.0f,-1.0f,    0.0f, 0.0f,
			-1.0f,-1.0f,-1.0f,    0.0f, 1.0f,
			                      
			-1.0f,-1.0f,-1.0f,    1.0f, 1.0f,  // -Z side
			 1.0f, 1.0f,-1.0f,    0.0f, 0.0f,
			 1.0f,-1.0f,-1.0f,    0.0f, 1.0f,
			-1.0f,-1.0f,-1.0f,    1.0f, 1.0f,
			-1.0f, 1.0f,-1.0f,    1.0f, 0.0f,
			 1.0f, 1.0f,-1.0f,    0.0f, 0.0f,
			                      
			-1.0f,-1.0f,-1.0f,    1.0f, 0.0f,  // -Y side
			 1.0f,-1.0f,-1.0f,    1.0f, 1.0f,
			 1.0f,-1.0f, 1.0f,    0.0f, 1.0f,
			-1.0f,-1.0f,-1.0f,    1.0f, 0.0f,
			 1.0f,-1.0f, 1.0f,    0.0f, 1.0f,
			-1.0f,-1.0f, 1.0f,    0.0f, 0.0f,
			                      
			-1.0f, 1.0f,-1.0f,    1.0f, 0.0f,  // +Y side
			-1.0f, 1.0f, 1.0f,    0.0f, 0.0f,
			 1.0f, 1.0f, 1.0f,    0.0f, 1.0f,
			-1.0f, 1.0f,-1.0f,    1.0f, 0.0f,
			 1.0f, 1.0f, 1.0f,    0.0f, 1.0f,
			 1.0f, 1.0f,-1.0f,    1.0f, 1.0f,
			                      
			 1.0f, 1.0f,-1.0f,    1.0f, 0.0f,  // +X side
			 1.0f, 1.0f, 1.0f,    0.0f, 0.0f,
			 1.0f,-1.0f, 1.0f,    0.0f, 1.0f,
			 1.0f,-1.0f, 1.0f,    0.0f, 1.0f,
			 1.0f,-1.0f,-1.0f,    1.0f, 1.0f,
			 1.0f, 1.0f,-1.0f,    1.0f, 0.0f,
			                      
			-1.0f, 1.0f, 1.0f,    0.0f, 0.0f,  // +Z side
			-1.0f,-1.0f, 1.0f,    0.0f, 1.0f,
			 1.0f, 1.0f, 1.0f,    1.0f, 0.0f,
			-1.0f,-1.0f, 1.0f,    0.0f, 1.0f,
			 1.0f,-1.0f, 1.0f,    1.0f, 1.0f,
			 1.0f, 1.0f, 1.0f,    1.0f, 0.0f,
		};

		Camera Camera = new Camera (Utils.DegreesToRadians (60f), 1f);

		Program () : base () {
			vboSkybox = new GPUBuffer<float> (presentQueue, cmdPool, VkBufferUsageFlags.VertexBuffer, box_vertices);

			cubemap = KTX.KTX.Load (presentQueue, cmdPool, cubemapPathes[0],
				VkImageUsageFlags.Sampled, VkMemoryPropertyFlags.DeviceLocal, true);
			cubemap.CreateView (VkImageViewType.ImageCube,VkImageAspectFlags.Color,6);
			cubemap.CreateSampler ();
							
			init ();

			model = new Model (dev, presentQueue, cmdPool, "../data/models/DamagedHelmet/glTF/DamagedHelmet.gltf");
			model.WriteMaterialsDescriptorSets (descLayoutTextures,
				ShaderBinding.Color,
				ShaderBinding.Normal,
				ShaderBinding.AmbientOcclusion,
				ShaderBinding.MetalRoughness,
				ShaderBinding.Emissive);
		}

		void init (VkSampleCountFlags samples = VkSampleCountFlags.Count4) { 
			descriptorPool = new DescriptorPool (dev, 2,
				new VkDescriptorPoolSize (VkDescriptorType.UniformBuffer, 2),
				new VkDescriptorPoolSize (VkDescriptorType.CombinedImageSampler, 2)
			);

			descLayoutMain = new DescriptorSetLayout (dev,
				new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Vertex|VkShaderStageFlags.Fragment, VkDescriptorType.UniformBuffer),
				new VkDescriptorSetLayoutBinding (1, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler));

			descLayoutTextures = new DescriptorSetLayout (dev, 
				new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),
				new VkDescriptorSetLayoutBinding (1, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),
				new VkDescriptorSetLayoutBinding (2, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),
				new VkDescriptorSetLayoutBinding (3, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),
				new VkDescriptorSetLayoutBinding (4, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler)
			);

			dsMain = descriptorPool.Allocate (descLayoutMain);
			dsSkybox = descriptorPool.Allocate (descLayoutMain);

			#region main pipeline creation
			pipeline = new Pipeline (dev,
				swapChain.ColorFormat,
				dev.GetSuitableDepthFormat (),
				VkPrimitiveTopology.TriangleList, samples);
			pipeline.Layout = new PipelineLayout (dev, descLayoutMain, descLayoutTextures);
			pipeline.Layout.AddPushConstants (
				new VkPushConstantRange (VkShaderStageFlags.Vertex, (uint)Marshal.SizeOf<Matrix4x4> ()),
				new VkPushConstantRange (VkShaderStageFlags.Fragment, (uint)Marshal.SizeOf<Model.PbrMaterial> (), 64)
			);
			pipeline.AddVertexBinding<Model.Vertex> (0);
			pipeline.SetVertexAttributes (0, VkFormat.R32g32b32Sfloat, VkFormat.R32g32b32Sfloat, VkFormat.R32g32Sfloat);
			pipeline.AddShader (VkShaderStageFlags.Vertex, "shaders/pbrtest.vert.spv");
			pipeline.AddShader (VkShaderStageFlags.Fragment, "shaders/pbrtest.frag.spv");
			pipeline.Activate ();
			#endregion

			#region skybox pipeline
			skyboxPL = new Pipeline (dev, pipeline.RenderPass, VkPrimitiveTopology.TriangleList, samples);
			skyboxPL.Layout = new PipelineLayout (dev, descLayoutMain);//should be share with the main pl
			skyboxPL.AddVertexBinding (0, 5 * sizeof(float));
			skyboxPL.SetVertexAttributes (0, VkFormat.R32g32b32Sfloat, VkFormat.R32g32Sfloat);
			skyboxPL.AddShader (VkShaderStageFlags.Vertex, "shaders/skybox.vert.spv");
			skyboxPL.AddShader (VkShaderStageFlags.Fragment, "shaders/skybox.frag.spv");
			skyboxPL.depthStencilState.depthTestEnable = false;
			skyboxPL.depthStencilState.depthWriteEnable = false;
			skyboxPL.Activate ();
			#endregion

			uboMats = new HostBuffer (dev, VkBufferUsageFlags.UniformBuffer, (ulong)Marshal.SizeOf<Matrices>());
			uboMats.Map ();//permanent map
			uboMatsSkybox = new HostBuffer (dev, VkBufferUsageFlags.UniformBuffer, (ulong)Marshal.SizeOf<Matrices>());
			uboMatsSkybox.Map ();//permanent map

			using (DescriptorSetWrites uboUpdate = new DescriptorSetWrites (dev)) {
				uboUpdate.AddWriteInfo (dsMain, descLayoutMain.Bindings[0], uboMats.Descriptor);
				uboUpdate.AddWriteInfo (dsMain, descLayoutMain.Bindings[1], cubemap.Descriptor);
				uboUpdate.Update ();
			}

			using (DescriptorSetWrites uboUpdate = new DescriptorSetWrites (dev)) {
				uboUpdate.AddWriteInfo (dsSkybox, descLayoutMain.Bindings[0], uboMatsSkybox.Descriptor);
				uboUpdate.AddWriteInfo (dsSkybox, descLayoutMain.Bindings[1], cubemap.Descriptor);
				uboUpdate.Update ();
			}
		}

		void buildCommandBuffers () {
			for (int i = 0; i < swapChain.ImageCount; ++i) { 								
                  	cmds[i]?.Free ();
				cmds[i] = cmdPool.AllocateCommandBuffer ();
				cmds[i].Start ();

				recordDraw (cmds[i], frameBuffers[i]);

				cmds[i].End ();				 
			}
		} 
		void recordDraw (CommandBuffer cmd, Framebuffer fb) { 
			pipeline.RenderPass.Begin (cmd, fb);

			cmd.SetViewport (fb.Width, fb.Height);
			cmd.SetScissor (fb.Width, fb.Height);

			skyboxPL.Bind (cmd);
			cmd.BindDescriptorSet (skyboxPL.Layout, dsSkybox);
			cmd.BindVertexBuffer (vboSkybox);
			cmd.Draw (36);

			pipeline.Bind (cmd);
			cmd.BindDescriptorSet (pipeline.Layout, dsMain);
			model.Bind (cmd);
			model.DrawAll (cmd, pipeline.Layout);

			pipeline.RenderPass.End (cmd);
		}

		void updateMatrices () {

			Camera.AspectRatio = (float)swapChain.Width / swapChain.Height;

			matrices.projection = Camera.Projection;
			matrices.view = Camera.View;
			matrices.model = Camera.Model;
			uboMats.Update (matrices, (uint)Marshal.SizeOf<Matrices> ());
			matrices.view *= Matrix4x4.CreateTranslation(-matrices.view.Translation);
			matrices.model = Matrix4x4.Identity;
			uboMatsSkybox.Update (matrices, (uint)Marshal.SizeOf<Matrices> ());
		}
			
		public override void Update () {
			updateMatrices ();
			updateRequested = false;
		}

		protected override void onMouseMove (double xPos, double yPos) {
			double diffX = lastMouseX - xPos;
			double diffY = lastMouseY - yPos;
			if (MouseButton[0]) {
				Camera.Rotate ((float)-diffX,(float)-diffY);
			} else if (MouseButton[1]) {
				Camera.Zoom ((float)diffY);
			}

			updateRequested = true;
		}

		protected override void onKeyDown (Key key, int scanCode, Modifier modifiers) {
			switch (key) {
				case Key.Up:
					if (modifiers.HasFlag (Modifier.Shift))
						matrices.lightPos += new Vector4 (0, 0, 1, 0);
					else
						Camera.Move (0, 0, 1);
					break;
				case Key.Down:
					if (modifiers.HasFlag (Modifier.Shift))
						matrices.lightPos += new Vector4 (0, 0, -1, 0);
					else
						Camera.Move (0, 0, -1);
					break;
				case Key.Left:
					if (modifiers.HasFlag (Modifier.Shift))
						matrices.lightPos += new Vector4 (1, 0, 0, 0);
					else
						Camera.Move (1, 0, 0);
					break;
				case Key.Right:
					if (modifiers.HasFlag (Modifier.Shift))
						matrices.lightPos += new Vector4 (-1, 0, 0, 0);
					else
						Camera.Move (-1, 0, 0);
					break;
				case Key.PageUp:
					if (modifiers.HasFlag (Modifier.Shift))
						matrices.lightPos += new Vector4 (0, 1, 0, 0);
					else
						Camera.Move (0, 1, 0);
					break;
				case Key.PageDown:
					if (modifiers.HasFlag (Modifier.Shift))
						matrices.lightPos += new Vector4 (0, -1, 0, 0);
					else
						Camera.Move (0, -1, 0);
					break;
				case Key.F1:
					if (modifiers.HasFlag (Modifier.Shift))
						matrices.exposure -= 0.3f;
					else
						matrices.exposure += 0.3f;
					break;
				case Key.F2:
					if (modifiers.HasFlag (Modifier.Shift))
						matrices.gamma -= 0.1f;
					else
						matrices.gamma += 0.1f;
					break;
				case Key.F3:
					if (Camera.Type == Camera.CamType.FirstPerson)
						Camera.Type = Camera.CamType.LookAt;
					else
						Camera.Type = Camera.CamType.FirstPerson;
					Console.WriteLine ($"camera type = {Camera.Type}");
					break;
				default:
					base.onKeyDown (key, scanCode, modifiers);
					return;
			}
			updateRequested = true;
		}

		protected override void OnResize () {

			updateMatrices ();

			if (frameBuffers!=null)
				for (int i = 0; i < swapChain.ImageCount; ++i)
					frameBuffers[i]?.Dispose ();

			frameBuffers = new Framebuffer[swapChain.ImageCount];

			for (int i = 0; i < swapChain.ImageCount; ++i) {
				frameBuffers[i] = new Framebuffer (pipeline.RenderPass, swapChain.Width, swapChain.Height,
					(pipeline.Samples == VkSampleCountFlags.Count1) ? new Image[] {
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

		protected override void Dispose (bool disposing) {
			if (disposing) {
				if (!isDisposed) {
					dev.WaitIdle ();
					for (int i = 0; i < swapChain.ImageCount; ++i)
						frameBuffers[i]?.Dispose ();
					model.Dispose ();
					vboSkybox.Dispose ();
					pipeline.Dispose ();
					skyboxPL.Dispose ();
					descLayoutMain.Dispose ();
					descLayoutTextures.Dispose ();
					descriptorPool.Dispose ();
					cubemap.Dispose ();

					uboMats.Dispose ();
					uboMatsSkybox.Dispose ();
				}
			}

			base.Dispose (disposing);
		}
	}
}
