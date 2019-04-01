﻿using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Glfw;
using VKE;
using Vulkan;
using Buffer = VKE.Buffer;

namespace ModelSample {
	class Program : VkWindow {
		static void Main (string[] args) {
			using (Program vke = new Program ()) {
				vke.Run ();
			}
		}

		struct Matrices {
			public Matrix4x4 projection;
			public Matrix4x4 view;
			public Matrix4x4 model;
			public Vector4 lightPos;
		}

		struct PushConstants {
			public Matrix4x4 matrix;
		}

		Matrices matrices;
		PushConstants pushCst;

		HostBuffer uboMats;

		DescriptorSetLayout descLayoutMatrix;
		DescriptorSetLayout descLayoutTextures;

		DescriptorPool descriptorPool;
		DescriptorSet descriptorSet;

		RenderPass renderPass;
		Framebuffer[] frameBuffers;

		PipelineLayout pipelineLayout;
		Pipeline pipeline;

		VkFormat depthFormat;
		Image depthTexture;

		float rotSpeed = 0.01f, zoomSpeed = 0.01f;
		double lastMouseX, lastMouseY;
		float rotX = -1.5f, rotY = 2.7f, rotZ = 0f;
		float zoom = 1.0f;

		Model helmet;

		Program () : base () {
		
			descriptorPool = new DescriptorPool (dev, 27,
				new VkDescriptorPoolSize (VkDescriptorType.UniformBuffer),
				new VkDescriptorPoolSize (VkDescriptorType.CombinedImageSampler,3*26)
				//new VkDescriptorPoolSize (VkDescriptorType.CombinedImageSampler),
				//new VkDescriptorPoolSize (VkDescriptorType.CombinedImageSampler)
			);				

			descLayoutMatrix = new DescriptorSetLayout (dev,
				new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Vertex, VkDescriptorType.UniformBuffer));

			descLayoutTextures = new DescriptorSetLayout (dev, 
				new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),
				new VkDescriptorSetLayoutBinding (1, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),
				new VkDescriptorSetLayoutBinding (2, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler)
			);

			descriptorSet = descriptorPool.Allocate (descLayoutMatrix);

			VkPushConstantRange pushConstantRange = new VkPushConstantRange { 
				stageFlags = VkShaderStageFlags.Vertex,
				size = (uint)Marshal.SizeOf<PushConstants>(),
				offset = 0
			};

			pipelineLayout = new PipelineLayout (dev, pushConstantRange, descLayoutMatrix, descLayoutTextures).Activate ();

			loadAssets ();

			uboMats = new HostBuffer (dev, VkBufferUsageFlags.UniformBuffer, matrices);

			depthFormat = dev.GetSuitableDepthFormat ();

			renderPass = new RenderPass (dev, swapChain.ColorFormat, depthFormat);

			frameBuffers = new Framebuffer[swapChain.ImageCount];

			pipeline = new Pipeline (pipelineLayout, renderPass);

			pipeline.vertexBindings.Add (new VkVertexInputBindingDescription (0, (uint)Marshal.SizeOf<Model.Vertex> ()));

			pipeline.vertexAttributes.Add (new VkVertexInputAttributeDescription (0, VkFormat.R32g32b32Sfloat));
			pipeline.vertexAttributes.Add (new VkVertexInputAttributeDescription (1, VkFormat.R32g32b32Sfloat, 3 * sizeof (float)));
			pipeline.vertexAttributes.Add (new VkVertexInputAttributeDescription (2, VkFormat.R32g32Sfloat, 6 * sizeof (float)));

			pipeline.shaders.Add (new ShaderInfo (VkShaderStageFlags.Vertex, "shaders/model.vert.spv"));
			pipeline.shaders.Add (new ShaderInfo (VkShaderStageFlags.Fragment, "shaders/model.frag.spv"));

			pipeline.Activate ();

			using (DescriptorSetWrites uboUpdate = new DescriptorSetWrites (dev)) {
				uboUpdate.AddWriteInfo (descriptorSet, descLayoutMatrix.Bindings[0], uboMats.Descriptor);
				uboUpdate.Update ();
			}

			matrices.lightPos = new Vector4 (0.0f, 0.0f, -2.0f, 1.0f);

			uboMats.Map ();//permanent map
			updateMatrices ();
		}

		protected override void Prepare () {

			if (depthTexture != null)
				depthTexture.Dispose ();

			depthTexture = new Image (dev, depthFormat, VkImageUsageFlags.DepthStencilAttachment,
					VkMemoryPropertyFlags.DeviceLocal, swapChain.Width, swapChain.Height);
			depthTexture.CreateView (VkImageViewType.Image2D, VkImageAspectFlags.Depth);

			for (int i = 0; i < swapChain.ImageCount; ++i) {
				if (frameBuffers[i] != null)
					frameBuffers[i].Destroy ();
			}

			for (int i = 0; i < swapChain.ImageCount; ++i) {
				frameBuffers[i] = new Framebuffer (renderPass, swapChain.Width, swapChain.Height,
						new VkImageView[] { swapChain.images[i].Descriptor.imageView, depthTexture.Descriptor.imageView });

				cmds[i] = cmdPool.AllocateCommandBuffer ();
				cmds[i].Start ();

				renderPass.Begin (cmds[i], frameBuffers[i]);

				cmds[i].SetViewport (swapChain.Width, swapChain.Height);
				cmds[i].SetScissor (swapChain.Width, swapChain.Height);

				cmds[i].BindDescriptorSet (pipelineLayout, descriptorSet);

				cmds[i].BindPipeline (pipeline);

				helmet.Bind (cmds[i]);
				helmet.DrawAll (cmds[i], pipelineLayout);

				renderPass.End (cmds[i]);

				cmds[i].End ();
			}
		}

		void loadAssets () {
			//helmet = new Model (dev, presentQueue, cmdPool, "/mnt/devel/vulkan/glTF-Sample-Models-master/2.0/Sponza/glTF/Sponza.gltf");
			helmet = new Model (dev, presentQueue, cmdPool, "data/DamagedHelmet.gltf");
			foreach (Model.Material mat in helmet.materials) {
				mat.descriptorSet = descriptorPool.Allocate (descLayoutTextures);
				using (DescriptorSetWrites uboUpdate = new DescriptorSetWrites (dev)) {
					uboUpdate.AddWriteInfo (mat.descriptorSet, descLayoutTextures.Bindings[0], helmet.textures[(int)mat.baseColorTexture].Descriptor);
					uboUpdate.AddWriteInfo (mat.descriptorSet, descLayoutTextures.Bindings[1], helmet.textures[(int)mat.normalTexture].Descriptor);
					uboUpdate.AddWriteInfo (mat.descriptorSet, descLayoutTextures.Bindings[2], helmet.textures[(int)mat.occlusionTexture].Descriptor);
					uboUpdate.Update ();
				}
			}

			helmet.PipelineLayout = pipelineLayout;
		}
		void updateMatrices () {
			matrices.projection = Matrix4x4.CreatePerspectiveFieldOfView (Utils.DegreesToRadians (60f), (float)swapChain.Width / (float)swapChain.Height, 0.01f, 1024.0f);
			//matrices.view = Matrix4x4.CreateLookAt (new Vector3 (0, 0, -1), new Vector3 (0, 0, 0), Vector3.UnitY);//Matrix4x4.CreateTranslation (0, 0, -2.5f);
			matrices.view = Matrix4x4.CreateTranslation (0, 0, -2.5f * zoom);
			matrices.model =
					Matrix4x4.CreateFromAxisAngle (Vector3.UnitX, rotX) *
					Matrix4x4.CreateFromAxisAngle (Vector3.UnitY, rotY) *
					Matrix4x4.CreateFromAxisAngle (Vector3.UnitZ, rotZ);

			uboMats.Update (matrices, (uint)Marshal.SizeOf<Matrices> ());
		}

		public override void Update () {
			updateMatrices ();
			updateRequested = false;
		}
		protected override void onMouseMove (double xPos, double yPos) {
			double diffX = lastMouseX - xPos;
			double diffY = lastMouseY - yPos;
			if (MouseButton[0]) {
				rotY -= rotSpeed * (float)diffX;
				rotX -= rotSpeed * (float)diffY;
			} else if (MouseButton[1]) {
				zoom += zoomSpeed * (float)diffY;
			}
			lastMouseX = xPos;
			lastMouseY = yPos;

			updateRequested = true;
		}


		protected override void Dispose (bool disposing) {
			pipeline.Destroy ();
			pipelineLayout.Dispose ();
			descLayoutMatrix.Dispose ();
			descLayoutTextures.Dispose ();
			for (int i = 0; i < swapChain.ImageCount; i++)
				frameBuffers[i].Destroy ();
			descriptorPool.Destroy ();
			renderPass.Destroy ();

			if (disposing) {
				helmet.Dispose ();
				depthTexture.Dispose ();
				uboMats.Dispose ();
			}

			base.Dispose (disposing);
		}
	}
}
