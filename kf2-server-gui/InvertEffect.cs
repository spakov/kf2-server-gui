﻿/* Source: https://stackoverflow.com/a/45096471 */

using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media.Effects;

namespace kf2_server_gui {
  class InvertEffect : ShaderEffect {
    private const string _kshaderAsBase64 =
  @"AAP///7/HwBDVEFCHAAAAE8AAAAAA///AQAAABwAAAAAAQAASAAAADAAAAADAAAAAQACADgAAAAA
AAAAaW5wdXQAq6sEAAwAAQABAAEAAAAAAAAAcHNfM18wAE1pY3Jvc29mdCAoUikgSExTTCBTaGFk
ZXIgQ29tcGlsZXIgMTAuMQCrUQAABQAAD6AAAIA/AAAAAAAAAAAAAAAAHwAAAgUAAIAAAAOQHwAA
AgAAAJAACA+gQgAAAwAAD4AAAOSQAAjkoAIAAAMAAAeAAADkgQAAAKAFAAADAAgHgAAA/4AAAOSA
AQAAAgAICIAAAP+A//8AAA==";

    private static readonly PixelShader _shader;

    static InvertEffect() {
      _shader = new PixelShader();
      _shader.SetStreamSource(new MemoryStream(Convert.FromBase64String(_kshaderAsBase64)));
    }

    public InvertEffect() {
      PixelShader = _shader;
      UpdateShaderValue(InputProperty);
    }

    public Brush Input {
      get { return (Brush) GetValue(InputProperty); }
      set { SetValue(InputProperty, value); }
    }

    public static readonly DependencyProperty InputProperty = ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(InvertEffect), 0);
  }
}
