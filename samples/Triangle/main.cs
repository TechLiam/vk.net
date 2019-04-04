﻿using System.Numerics;
using System.Runtime.InteropServices;
using VKE;
using Vulkan;

namespace ModelSample {
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
            public Matrix4x4 model;
        }
        struct Vertex {
            Vector3 position;
            Vector3 color;

            public Vertex (float x, float y, float z, float r, float g, float b) {
                position = new Vector3 (x, y, z);
                color = new Vector3 (r, g, b);
            }
        }

        Matrices matrices;

        HostBuffer ibo;
        HostBuffer vbo;
        HostBuffer uboMats;

        DescriptorPool descriptorPool;
        DescriptorSetLayout dsLayout;
        DescriptorSet descriptorSet;
				        
        Framebuffer[] frameBuffers;		        
        Pipeline pipeline;

        Vertex[] vertices = {
            new Vertex ( 1.0f,  1.0f, 0.0f ,  1.0f, 0.0f, 0.0f),
            new Vertex (-1.0f,  1.0f, 0.0f ,  0.0f, 1.0f, 0.0f),
            new Vertex ( 0.0f, -1.0f, 0.0f ,  0.0f, 0.0f, 1.0f),
        };
        ushort[] indices = new ushort[] { 0, 1, 2 };

        Program () : base () {
            vbo = new HostBuffer<Vertex> (dev, VkBufferUsageFlags.VertexBuffer, vertices);
            ibo = new HostBuffer<ushort> (dev, VkBufferUsageFlags.IndexBuffer, indices);
            uboMats = new HostBuffer (dev, VkBufferUsageFlags.UniformBuffer, matrices);
            
			descriptorPool = new DescriptorPool (dev, 1, new VkDescriptorPoolSize (VkDescriptorType.UniformBuffer));
            dsLayout = new DescriptorSetLayout (dev,
				new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Vertex|VkShaderStageFlags.Fragment, VkDescriptorType.UniformBuffer));
            
			descriptorSet = descriptorPool.Allocate (dsLayout);

            pipeline = new Pipeline (dev,
				swapChain.ColorFormat,
				dev.GetSuitableDepthFormat (),
				VkPrimitiveTopology.TriangleList, VkSampleCountFlags.Count1);
			pipeline.Layout = new PipelineLayout (dev, dsLayout);

			pipeline.AddVertexBinding<Vertex> (0);
			pipeline.SetVertexAttributes (0, VkFormat.R32g32b32Sfloat, VkFormat.R32g32b32Sfloat);

            pipeline.AddShader (VkShaderStageFlags.Vertex, "shaders/triangle.vert.spv");
            pipeline.AddShader (VkShaderStageFlags.Fragment, "shaders/triangle.frag.spv");

            pipeline.Activate ();

            using (DescriptorSetWrites uboUpdate = new DescriptorSetWrites (dev)) {
                uboUpdate.AddWriteInfo (descriptorSet, dsLayout.Bindings[0], uboMats.Descriptor);
                uboUpdate.Update ();
            }

            uboMats.Map ();
        }

        public override void Update () {
            matrices.projection = Matrix4x4.CreatePerspectiveFieldOfView (Utils.DegreesToRadians (60f), (float)swapChain.Width / (float)swapChain.Height, 0.1f, 256.0f);
            matrices.view = Matrix4x4.CreateTranslation (0, 0, -2.5f * zoom);
            matrices.model =
                Matrix4x4.CreateFromAxisAngle (Vector3.UnitZ, rotZ) *
                Matrix4x4.CreateFromAxisAngle (Vector3.UnitY, rotY) *
                Matrix4x4.CreateFromAxisAngle (Vector3.UnitX, rotX);

            uboMats.Update (matrices, (uint)Marshal.SizeOf<Matrices> ());
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

        protected override void OnResize () {

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

                cmds[i] = cmdPool.AllocateCommandBuffer ();
                cmds[i].Start ();

                pipeline.RenderPass.Begin (cmds[i], frameBuffers[i]);

                cmds[i].SetViewport (swapChain.Width, swapChain.Height);
                cmds[i].SetScissor (swapChain.Width, swapChain.Height);

                cmds[i].BindDescriptorSet (pipeline.Layout, descriptorSet);

                cmds[i].BindPipeline (pipeline);

                cmds[i].BindVertexBuffer (vbo);
                cmds[i].BindIndexBuffer (ibo, VkIndexType.Uint16);
                cmds[i].DrawIndexed ((uint)indices.Length);

                pipeline.RenderPass.End (cmds[i]);

                cmds[i].End ();
            }
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
					vbo.Dispose ();
					ibo.Dispose ();
					uboMats.Dispose ();
				}
			}

			base.Dispose (disposing);
		}
    }
}
