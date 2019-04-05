﻿using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Glfw;
using VKE;
using Vulkan;
using Buffer = VKE.Buffer;

namespace TextureCube {
	class Program : VkWindow {
		static void Main (string[] args) {
			using (Program vke = new Program ()) {
				vke.Run ();
			}
		}

		float rotSpeed = 0.01f, zoomSpeed = 0.01f;
		float rotX, rotY, rotZ = 0f, zoom = 1f;

		struct Matrices {
			public Matrix4x4 projection;
			public Matrix4x4 view;
		}

		Matrices matrices;

		HostBuffer 			uboMats;
		GPUBuffer<float> 	vbo;
		DescriptorPool 		descriptorPool;
		DescriptorSetLayout dsLayout;
		DescriptorSet 		descriptorSet;
		Pipeline 			pipeline;
		Framebuffer[] 		frameBuffers;

		Image texture;
		Image nextTexture;

		static float[] g_vertex_buffer_data = {
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
		int currentImgIndex = 0;
		string[] imgPathes = {
			"../data/textures/papermill.ktx",
			"../data/textures/cubemap_yokohama_bc3_unorm.ktx",
			"../data/textures/gcanyon_cube.ktx",
			"../data/textures/pisa_cube.ktx",
			"../data/textures/uffizi_cube.ktx",
		};


		Program () : base () {
			vbo = new GPUBuffer<float> (presentQueue, cmdPool, VkBufferUsageFlags.VertexBuffer, g_vertex_buffer_data);

			descriptorPool = new DescriptorPool (dev, 1,
				new VkDescriptorPoolSize (VkDescriptorType.UniformBuffer),
				new VkDescriptorPoolSize (VkDescriptorType.CombinedImageSampler)
			);

			dsLayout = new DescriptorSetLayout (dev, VkDescriptorSetLayoutCreateFlags.None,
				new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Vertex, VkDescriptorType.UniformBuffer),
				new VkDescriptorSetLayoutBinding (1, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler));

			descriptorSet = descriptorPool.Allocate (dsLayout);

			pipeline = new Pipeline (dev,
				swapChain.ColorFormat,
				dev.GetSuitableDepthFormat (),
				VkPrimitiveTopology.TriangleList, VkSampleCountFlags.Count1);

			pipeline.Layout = new PipelineLayout (dev, dsLayout);
			pipeline.AddVertexBinding (0, 5 * sizeof(float));
			pipeline.SetVertexAttributes (0, VkFormat.R32g32b32Sfloat, VkFormat.R32g32Sfloat);

			pipeline.AddShader (VkShaderStageFlags.Vertex, "shaders/skybox.vert.spv");
			pipeline.AddShader (VkShaderStageFlags.Fragment, "shaders/skybox.frag.spv");

			pipeline.Activate ();

			uboMats = new HostBuffer (dev, VkBufferUsageFlags.UniformBuffer, matrices);
			uboMats.Map ();//permanent map

			using (DescriptorSetWrites2 uboUpdate = new DescriptorSetWrites2 (dev)) {
				uboUpdate.AddWriteInfo (descriptorSet, dsLayout.Bindings[0], uboMats.Descriptor);
				uboUpdate.Update ();
			}

			loadTexture (imgPathes[currentImgIndex]);
			updateTextureSet ();
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
			cmd.BindDescriptorSet (pipeline.Layout, descriptorSet);

			pipeline.Bind (cmd);

			cmd.BindVertexBuffer (vbo, 0);
			cmd.Draw (36);

			pipeline.RenderPass.End (cmd);
		}

		//in the thread of the keyboard
		void loadTexture (string path) {
			if (path.EndsWith("ktx", StringComparison.OrdinalIgnoreCase))
				nextTexture = KTX.KTX.Load (presentQueue, cmdPool, path,
					VkImageUsageFlags.Sampled, VkMemoryPropertyFlags.DeviceLocal, true);
			else
				nextTexture = Image.Load (dev, path);
			updateRequested = true;
		}

		//in the main vulkan thread
		void updateTextureSet (){ 
			nextTexture.CreateView (VkImageViewType.ImageCube,VkImageAspectFlags.Color,6);
			nextTexture.CreateSampler ();

			nextTexture.Descriptor.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
			using (DescriptorSetWrites2 uboUpdate = new DescriptorSetWrites2 (dev)) {
				uboUpdate.AddWriteInfo (descriptorSet, dsLayout.Bindings[1], nextTexture.Descriptor);
				uboUpdate.Update ();
			}

			texture?.Dispose ();
			texture = nextTexture;
			nextTexture = null;
		}
		void updateMatrices () { 
			matrices.projection = Matrix4x4.CreatePerspectiveFieldOfView (Utils.DegreesToRadians (60f), (float)swapChain.Width / (float)swapChain.Height, 0.1f, 256.0f);
			matrices.view =
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitZ, rotZ) *
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitY, rotY) *
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitX, rotX);

			uboMats.Update (matrices, (uint)Marshal.SizeOf<Matrices> ());
		}

		protected override void configureEnabledFeatures (ref VkPhysicalDeviceFeatures features) {
			base.configureEnabledFeatures (ref features);
			features.textureCompressionBC = true;
		}

		public override void Update () {
			if (nextTexture != null) {
				dev.WaitIdle ();
				updateTextureSet ();
				buildCommandBuffers ();
			} else 
				updateMatrices ();

			updateRequested = false;
		}

		protected override void onMouseMove (double xPos, double yPos) {
			double diffX = lastMouseX - xPos;
			double diffY = lastMouseY - yPos;
			if (MouseButton[0]) {
				rotY -= rotSpeed * (float)diffX;
				rotX += rotSpeed * (float)diffY;
			} else if (MouseButton[1]) {
				zoom += zoomSpeed * (float)diffY;
			}

			updateRequested = true;
		}
		protected override void onKeyDown (Key key, int scanCode, Modifier modifiers) {
			switch (key) {
				case Key.Space:
					currentImgIndex++;
					if (currentImgIndex == imgPathes.Length)
						currentImgIndex = 0;
					loadTexture (imgPathes[currentImgIndex]);
					break;
				default:
					base.onKeyDown (key, scanCode, modifiers);
					break;
			}
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
					pipeline.Dispose ();
					dsLayout.Dispose ();
					for (int i = 0; i < swapChain.ImageCount; i++)
						frameBuffers[i].Dispose ();
					descriptorPool.Dispose ();
					texture.Dispose ();
					uboMats.Dispose ();

					vbo.Dispose ();
				}
			}

			base.Dispose (disposing);
		}
	}
}
