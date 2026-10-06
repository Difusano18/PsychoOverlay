# Implementation notes

The WinForms app hosts WebView2 and WebGL. Mode 6 captures a user-selected monitor with `getDisplayMedia`, processes its frames, and covers the selected display. It rejects window and browser-tab capture. `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` prevents feedback from the overlay.

The mode 6 fragment shader follows `NoxusReferences/NilkScreenDistortionShader.fx` in source order: coordinate warp, two sine-wave offsets, Perlin-driven previous-frame blend, center blur, luminance palette mapping, vignette, overlay blend, and final intensity blend. The previous-frame contribution is disabled until a history frame exists. The center blur skips its 12 texture reads when the blur mask is zero. The only overlay texture is transparent.

`NilkShaderPalettes.json` is embedded into the app. The C# controller owns the run timer, intensity curve, palette choice, and palette intervals. The curve reaches full intensity at minute 18, remains full through minute 54, and fades to zero at minute 60. Palette changes are immediate; shuffle intervals use the source manager's independent 1/3, 1/6, 1/9, and 1/15 probability checks, with later matches overriding earlier durations. The WebGL page receives only time, intensity, the current eight colors, and the running state.

The C# timer starts when mode 6 is selected, freezes while paused, and resets when another mode is selected. A randomized palette schedule is generated once per run using the source manager's interval rules; modes 6 and 7 use the same schedule, so seeking to a timestamp selects the palette mode 6 would show there. The JavaScript render loop interpolates between C# time updates so the shader clock remains smooth. Captured frames update with `texSubImage2D`; the canvas remains capped at the source resolution and 2.5 million pixels.

Mode 7 uses the same shader as mode 6 and seeks its clock to 38:00, 48:30, 52:00, or 58:30. Animation and palette timing continue from the selected point. `Ctrl+Alt+PageUp` and `Ctrl+Alt+PageDown` seek to the next or previous point and clamp at the first and last phase. Selecting mode 6 from mode 7 starts a fresh 60-minute run.

Capture requests `cursor: never`. The native Windows cursor is hidden over the selected monitor, and the shader draws one cursor at the corresponding warped coordinates. Cursor polling sends updates only when the position changes. The overlay returns foreground focus to the previously active app after the picker closes so monitor capture continues through Alt+Tab.
