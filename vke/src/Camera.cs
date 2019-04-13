﻿using System;
using System.Numerics;
using System.Runtime.InteropServices;

using VK;


namespace VKE {
	public class Camera {
		public enum CamType {LookAt, FirstPerson};

		float fov, aspectRatio, zNear = 0.1f, zFar = 256f, zoom = 1.0f;
		float moveSpeed = 1, rotSpeed = 0.01f, zoomSpeed = 0.01f;

		Vector3 rotation = new Vector3 (-1.5f, 2.7f, 0f);
		Vector3 position = new Vector3 (0, 0, -2);
		Matrix4x4 model = Matrix4x4.Identity;

		public CamType Type;
		
		public float AspectRatio {
			get { return aspectRatio; }
			set {
				aspectRatio = value;
				Update ();
			}
		}
		public float FieldOfView {
			get { return fov; }
			set {
				fov = value;
				Update ();
			}
		}
		public Matrix4x4 Perspective {
			get { return Matrix4x4.CreatePerspectiveFieldOfView (fov, aspectRatio, zNear, zFar); }
		}

		public Camera (float fieldOfView, float aspectRatio) {
			fov = fieldOfView;
			this.aspectRatio = aspectRatio;
			Update ();
		}

		public void Rotate (float x, float y, float z = 0) {
			rotation.Y += rotSpeed * x;
			rotation.X += rotSpeed * y;
			Update ();
		}
		public void Move (float x, float y, float z = 0) {
			position.X += moveSpeed * x;
			position.Y += moveSpeed * y;
			position.Z += moveSpeed * z;
			Update ();
		}
		public void Zoom (float factor) {
			zoom += zoomSpeed * factor;
			Update ();
		}

		public Matrix4x4 Projection { get; private set;}
		public Matrix4x4 View { get; private set;}
		public Matrix4x4 Model {
			get { return model; }
			set {
				model = value;
				Update ();
			} 
		}

		public Matrix4x4 SkyboxView {
			get { 
				return
					Matrix4x4.CreateFromAxisAngle (Vector3.UnitZ, rotation.Z) *
					Matrix4x4.CreateFromAxisAngle (Vector3.UnitY, rotation.Y) *
					Matrix4x4.CreateFromAxisAngle (Vector3.UnitX, rotation.X);
			}
		}

		public void Update () { 
			Projection = Matrix4x4.CreatePerspectiveFieldOfView (fov, aspectRatio, zNear, zFar);
			Matrix4x4 rot =
					Matrix4x4.CreateFromAxisAngle (Vector3.UnitX, rotation.X) *
					Matrix4x4.CreateFromAxisAngle (Vector3.UnitY, rotation.Y) *
					Matrix4x4.CreateFromAxisAngle (Vector3.UnitZ, rotation.Z);
			if (Type == CamType.LookAt) {
				View =	Matrix4x4.CreateFromAxisAngle (Vector3.UnitX, rotation.X) *
						Matrix4x4.CreateFromAxisAngle (Vector3.UnitY, rotation.Y) *
						Matrix4x4.CreateFromAxisAngle (Vector3.UnitZ, rotation.Z) *
						Matrix4x4.CreateTranslation (position);
			} else {
				View =	Matrix4x4.CreateTranslation (position) *
						Matrix4x4.CreateFromAxisAngle (Vector3.UnitZ, rotation.Z) *
						Matrix4x4.CreateFromAxisAngle (Vector3.UnitY, rotation.Y) *
						Matrix4x4.CreateFromAxisAngle (Vector3.UnitX, rotation.X);
			}
		}
	}
}
