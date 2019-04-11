﻿//
// Utils.cs
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
using System.Runtime.InteropServices;

namespace VK {
	internal static class LoadingUtils {
		public static IntPtr GetDelegate (VkInstance inst, string name) {
			byte[] n = System.Text.Encoding.UTF8.GetBytes (name + '\0');
			GCHandle hnd = GCHandle.Alloc (n, GCHandleType.Pinned);
			IntPtr del = Vk.vkGetInstanceProcAddr (inst, hnd.AddrOfPinnedObject ());
			if (del == IntPtr.Zero)
				Console.WriteLine ("instance function pointer not found for " + name);
			hnd.Free ();
			return del;
		}
		/// <summary>
		/// try to get device function handle if available
		/// </summary>
		public static void GetDelegate (VkDevice dev, string name, ref IntPtr ptr) {
			byte[] n = System.Text.Encoding.UTF8.GetBytes (name + '\0');
			GCHandle hnd = GCHandle.Alloc (n, GCHandleType.Pinned);
			IntPtr del = Vk.vkGetDeviceProcAddr (dev, hnd.AddrOfPinnedObject ());
			if (del == IntPtr.Zero)
				Console.WriteLine ("device function pointer not found for " + name);
			else
				ptr = del;
			hnd.Free ();			
		} 
	} 
}
