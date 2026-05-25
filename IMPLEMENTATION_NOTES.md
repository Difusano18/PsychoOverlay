# PsychoOverlay implementation notes

The original Wrath of the Gods Nilk shader is a full-screen post-processing shader. It samples the current screen texture, the previous screen texture, a noise texture, and an overlay texture, then applies wavy coordinate distortion, datamosh-like blending with the previous frame, palette remapping, and a vignette. A truly identical effect requires access to the rendered game framebuffer.

This standalone Windows overlay cannot directly distort windows behind it while staying genuinely transparent. Therefore, the practical approach is a **click-through, always-on-top transparent WinForms overlay** that draws low-opacity Nilk-inspired layers above the desktop: psychedelic texture currents, wavy bands, chromatic trails, palette glow particles, soft void cells, and vignette. The default opacity must be kept low so it does not “перебиває все”.

The overlay will include global hotkeys through `RegisterHotKey` because a click-through window usually does not receive normal keyboard events reliably. Controls are: `Ctrl+Alt+Up` to increase intensity, `Ctrl+Alt+Down` to decrease intensity, `Ctrl+Alt+Space` to toggle pause, and `Ctrl+Alt+Esc` to exit. The program also shows a small initial hint for a few seconds, rendered with very low opacity.

The code uses only .NET 8 WinForms and GDI+, with copied texture assets under `Assets/Textures/...` when available. If those assets are missing, procedural fallback visuals still work.
