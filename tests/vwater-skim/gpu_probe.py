"""Execute the project's HLSL in software OpenGL, independently of Unity.

Requires numpy, PyOpenGL, Mesa EGL, glslangValidator and spirv-dis.
This verifies compute outputs, not Unity's import, material or rendered appearance.
Only the unused UnityCG include is omitted; the kernel body is unmodified.
"""
import json
import os
from pathlib import Path
import re
import struct
import subprocess
import tempfile

os.environ.setdefault("PYOPENGL_PLATFORM", "egl")
os.environ.setdefault("EGL_PLATFORM", "surfaceless")
os.environ.setdefault("LIBGL_ALWAYS_SOFTWARE", "1")
import numpy as np
from OpenGL import EGL, GL
from OpenGL.GL.ARB.gl_spirv import glSpecializeShaderARB

ROOT = Path(__file__).resolve().parents[2]
N = 512


class Probe:
    def __init__(self):
        self.display = EGL.eglGetDisplay(EGL.EGL_DEFAULT_DISPLAY)
        EGL.eglInitialize(self.display, None, None)
        EGL.eglBindAPI(EGL.EGL_OPENGL_API)
        attrs = (EGL.EGLint * 5)(EGL.EGL_SURFACE_TYPE, EGL.EGL_PBUFFER_BIT,
                                EGL.EGL_RENDERABLE_TYPE, EGL.EGL_OPENGL_BIT, EGL.EGL_NONE)
        configs, count = (EGL.EGLConfig * 1)(), EGL.EGLint()
        EGL.eglChooseConfig(self.display, attrs, configs, 1, count)
        self.context = EGL.eglCreateContext(self.display, configs[0], EGL.EGL_NO_CONTEXT, None)
        EGL.eglMakeCurrent(self.display, EGL.EGL_NO_SURFACE, EGL.EGL_NO_SURFACE, self.context)
        self.renderer = GL.glGetString(GL.GL_RENDERER).decode()
        source = (ROOT / "Assets/VWater/Shaders/VWaterCompute.compute").read_text(encoding="utf-8-sig")
        source = source.replace('#include "UnityCG.cginc"', '')
        with tempfile.TemporaryDirectory(prefix="vwater-hlsl-") as tmp:
            path = Path(tmp) / "water.comp.hlsl"
            path.write_text(source)
            # Compile both entry points, with the original HLSL array layout.
            for kernel in ("CSWater", "CSOffsetBuffers"):
                spv = Path(tmp) / (kernel + ".spv")
                subprocess.run(["glslangValidator", "-D", "-V", "-S", "comp", "-e", kernel,
                                "--auto-map-bindings", str(path), "-o", str(spv)],
                               check=True, capture_output=True)
                if kernel != "CSWater":
                    continue
                assembly = subprocess.check_output(["spirv-dis", str(spv)], text=True)
                member_names = dict(re.findall(r'OpMemberName %_Global (\d+) "([^"]+)"', assembly))
                self.offsets = {member_names[i]: int(offset) for i, offset in
                                re.findall(r'OpMemberDecorate %_Global (\d+) Offset (\d+)', assembly)}
                self.bindings = {name: int(binding) for name, binding in
                                 re.findall(r'OpDecorate %(\w+) Binding (\d+)', assembly)}
                shader = GL.glCreateShader(GL.GL_COMPUTE_SHADER)
                binary = spv.read_bytes()
                GL.glShaderBinary(1, [shader], GL.GL_SHADER_BINARY_FORMAT_SPIR_V, binary, len(binary))
                glSpecializeShaderARB(shader, b"CSWater", 0, None, None)
                assert GL.glGetShaderiv(shader, GL.GL_COMPILE_STATUS), GL.glGetShaderInfoLog(shader)
                self.program = GL.glCreateProgram()
                GL.glAttachShader(self.program, shader)
                GL.glLinkProgram(self.program)
                assert GL.glGetProgramiv(self.program, GL.GL_LINK_STATUS), GL.glGetProgramInfoLog(self.program)
                GL.glDeleteShader(shader)
        GL.glUseProgram(self.program)
        self.textures = list(map(int, GL.glGenTextures(3)))
        for texture in self.textures:
            GL.glBindTexture(GL.GL_TEXTURE_2D, texture)
            GL.glTexStorage2D(GL.GL_TEXTURE_2D, 1, GL.GL_RGBA16F, N, N)
        self.ubo = GL.glGenBuffers(1)
        GL.glBindBuffer(GL.GL_UNIFORM_BUFFER, self.ubo)
        GL.glBindBufferBase(GL.GL_UNIFORM_BUFFER, self.bindings['_'], self.ubo)

    def clear(self):
        for texture in self.textures:
            GL.glClearTexImage(texture, 0, GL.GL_RGBA, GL.GL_FLOAT, np.zeros(4, np.float32))

    def run(self, steps, gain=3, height=1.9, velocity=0, pressure=.3, enabled=True, single=False):
        water, source, old = self.textures
        payload = bytearray(max(self.offsets.values()) + 16)
        def put(name, fmt, *values):
            struct.pack_into(fmt, payload, self.offsets[name], *values)
        put('BUFFER_SIZE', 'i', N)
        put('deltaTime', 'f', 1 / 60)
        # Actual saved camera-following universe, also exercises off-center sampling.
        put('worldPos', '2f', 4.5, -6)
        put('worldSize', 'f', 24)
        strength = .9 / (1 + (height / 1.2) ** 2) * gain
        if enabled:
            for slot, x in enumerate([0] if single else [-.54, .54]):
                struct.pack_into('4f', payload, self.offsets['skimPositionRadiusAmplitude'] + 16 * slot,
                                 x, 0, .4 + .18 * height, np.clip(-pressure * strength, -1, 1))
                struct.pack_into('4f', payload, self.offsets['skimVelocityFoam'] + 16 * slot,
                                 velocity, 0, np.clip(.55 * strength, 0, 1), 1)
        GL.glBufferData(GL.GL_UNIFORM_BUFFER, len(payload), bytes(payload), GL.GL_DYNAMIC_DRAW)
        for _ in range(steps):
            GL.glCopyImageSubData(water, GL.GL_TEXTURE_2D, 0, 0, 0, 0,
                                  source, GL.GL_TEXTURE_2D, 0, 0, 0, 0, N, N, 1)
            for name, texture in [('WaterBuffer', water), ('SourceBuffer', source), ('OldBuffer', old)]:
                GL.glBindImageTexture(self.bindings[name], texture, 0, False, 0,
                                      GL.GL_READ_WRITE, GL.GL_RGBA16F)
            GL.glDispatchCompute(16, 16, 1)
            GL.glMemoryBarrier(GL.GL_ALL_BARRIER_BITS)
            GL.glCopyImageSubData(source, GL.GL_TEXTURE_2D, 0, 0, 0, 0,
                                  old, GL.GL_TEXTURE_2D, 0, 0, 0, 0, N, N, 1)
        GL.glFinish()
        GL.glBindTexture(GL.GL_TEXTURE_2D, water)
        data = np.frombuffer(GL.glGetTexImage(GL.GL_TEXTURE_2D, 0, GL.GL_RGBA, GL.GL_FLOAT), np.float32)
        data = data.reshape(N, N, 4)
        assert np.isfinite(data).all(), "Nonfinite compute output"
        assert abs(data[..., 0]).max() <= 2 and abs(data[..., 2:]).max() <= 2, "Unbounded waves"
        assert data[..., 1].min() >= 0 and data[..., 1].max() <= 1, "Unbounded foam"
        return data.copy()


def metrics(data):
    return dict(min_height=float(data[..., 0].min()), max_height=float(data[..., 0].max()),
                max_foam=float(data[..., 1].max()), max_normal=float(abs(data[..., 2:]).max()))


def main():
    probe = Probe()
    result = {'renderer': probe.renderer, 'scope': 'Actual HLSL compute, software OpenGL; not Unity rendering'}
    def case(name, steps, **kwargs):
        probe.clear()
        data = probe.run(steps, **kwargs)
        result[name] = metrics(data)
        print(name, json.dumps(result[name]))
        return data
    zero = case('zero_gain_300_steps', 300, gain=0)
    assert np.count_nonzero(zero) == 0, "Zero gain must leave a cleared buffer flat"
    baseline = case('original_strength_150_steps', 150, gain=1)
    tuned = case('gain_3_300_steps', 300)
    assert .07 < baseline[..., 1].max() < .10, "Baseline reproduction changed"
    assert tuned[..., 1].max() > .3, "Tuned input must produce visible foam potential"
    assert abs(tuned[..., 2:]).max() > abs(baseline[..., 2:]).max() * 2, "Gain must affect normals"
    raised = case('raised_5_5m_300_steps', 300, height=5.5)
    assert raised[..., 1].max() < tuned[..., 1].max() / 2, "Height attenuation lost"
    # Isolate direct wake foam: pressure waves also generate foam symmetrically.
    wake = case('positive_x_wake_120_steps', 120, velocity=.6, single=True, pressure=0)
    world_x = (np.arange(N) / N - .5) * 24 + 4.5
    behind = wake[:, world_x < 0, 1].sum()
    ahead = wake[:, world_x > 0, 1].sum()
    assert behind > ahead * 1.5, "Wake must trail velocity"
    foam_only = case('foam_only_180_steps', 180, pressure=0)
    fading = probe.run(300, enabled=False)
    result['foam_only_after_300_off_steps'] = metrics(fading)
    assert fading[..., 1].max() < foam_only[..., 1].max() * .3, "Foam must fade after input stops"
    case('maximum_controls_180_steps', 180, gain=8, pressure=1, velocity=20)
    path = ROOT / 'Diagnostics/Downwash_20261103/gpu-verification.json'
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(result, indent=2) + '\n')
    print('PASS: zero input, original weakness, gain, height, wake direction, fade and finite safety bounds.')
    print('Report:', path)


if __name__ == '__main__':
    main()
