﻿//
// ExtensionMethods.cs
//
// Author:
//       Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// Copyright (c) 2019 jp
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace VKE {
    public static class ExtensionMethods {
		public static bool AreEquals (this byte[] b, byte[] other) {
			if (b.Length != other.Length)
				return false;
			for (int i = 0; i < b.Length; i++) {
				if (b[i] != other[i])
					return false;
			}
			return true;
		}

		public static Dictionary<object, GCHandle> handles = new Dictionary<object, GCHandle>();
        public static Vector3 Transform (this Vector3 v, ref Matrix4x4 mat, bool translate = false) {
            Vector4 v4 = Vector4.Transform (new Vector4 (v, translate ? 1f : 0f), mat);
            return new Vector3 (v4.X, v4.Y, v4.Z);
        }
        public static IntPtr Pin (this object obj) {
            if (handles.ContainsKey (obj)) 
                Debug.WriteLine ("Pinning already pinned object: {0}", obj);
                
            GCHandle hnd = GCHandle.Alloc (obj, GCHandleType.Pinned);
            handles.Add (obj, hnd);
            return hnd.AddrOfPinnedObject ();
        }
        public static void Unpin (this object obj) {
            if (!handles.ContainsKey (obj)) {
                Debug.WriteLine ("Trying to unpin {0}, but object has not been pinned.", obj);
                return;
            }
            handles[obj].Free ();
            handles.Remove (obj);
        }

    }
}
