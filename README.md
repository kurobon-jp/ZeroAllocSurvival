# Zero Alloc Survival

[日本語](README.ja.md)

Zero Alloc Survival is a survival game for testing [LitheEcs](https://github.com/kurobon-jp/LitheEcs) and [LocalAvoidance2D](https://github.com/kurobon-jp/LocalAvoidance2D) under high-load conditions close to those of a real game. It aims for 0 B/frame of managed allocations during steady-state execution after warmup.

It combines LitheEcs with Burst/Jobs-based 2D local avoidance and batched rendering to evaluate API design, runtime behavior, Unity integration, and performance with large numbers of entities.

Some resources are preallocated and warmed up at startup.

This is a technical validation project. It does not claim that managed allocations should be eliminated completely from every Unity game.

<video controls src="media/video.mp4" muted="false"></video>

## Requirements

- Unity `6000.5.1f1`
- A platform supported by Unity Burst and Collections

## Evaluation Conditions

Evaluating zero-allocation behavior requires an IL2CPP Development Build with `Development Build` enabled. Mono builds and the Unity Editor do not represent the zero-allocation conditions targeted by this project.

## Performance

- Measures enemy count, average FPS, average allocations per frame, total allocations during the interval, and frame count every five seconds by default
- Maximum number of spawned enemies: `10,000`

### Galaxy S23

| key | value |
| --- | --- |
| unity | 6000.5.1f1 |
| platform | Android |
| editor | False |
| development | True |
| os | Android OS 16 / API-36 (BP4A.251205.006/S9110ZHS8FZG1) |
| cpu | ARM64 FP ASIMD AES |
| cpu_cores | 8 |
| memory_mb | 7072 |
| gpu | Adreno (TM) 740 |
| graphics_api | Vulkan |

---

| elapsed_seconds | enemies | fps | alloc_bytes_per_frame | alloc_bytes_total | frames |
| --- | --- | --- | --- | --- | --- |
| 5.00 | 48 | 57.4 | 0 | 0 | 287 |
| 10.01 | 96 | 59.9 | 0 | 0 | 300 |
| 15.01 | 139 | 59.9 | 0 | 0 | 300 |
| 20.02 | 135 | 59.9 | 0 | 0 | 300 |
| 25.03 | 145 | 59.9 | 0 | 0 | 300 |
| 30.03 | 138 | 59.9 | 0 | 0 | 300 |
| 35.04 | 146 | 59.9 | 0 | 0 | 300 |
| 40.04 | 142 | 59.9 | 0 | 0 | 300 |
| 45.05 | 116 | 59.9 | 0 | 0 | 300 |
| 50.05 | 125 | 59.9 | 0 | 0 | 300 |
| 55.06 | 211 | 59.9 | 0 | 0 | 300 |
| 60.07 | 347 | 59.9 | 0 | 0 | 300 |
| 65.07 | 373 | 59.9 | 0 | 0 | 300 |
| 70.08 | 445 | 59.9 | 0 | 0 | 300 |
| 75.08 | 484 | 59.9 | 0 | 0 | 300 |
| 80.09 | 512 | 59.9 | 0 | 0 | 300 |
| 85.10 | 546 | 59.9 | 0 | 0 | 300 |
| 90.10 | 539 | 59.9 | 0 | 0 | 300 |
| 95.11 | 537 | 59.9 | 0 | 0 | 300 |
| 100.11 | 561 | 59.9 | 0 | 0 | 300 |
| 105.12 | 557 | 59.9 | 0 | 0 | 300 |
| 110.13 | 527 | 59.9 | 0 | 0 | 300 |
| 115.13 | 2309 | 59.9 | 0 | 0 | 300 |
| 120.14 | 2998 | 59.9 | 0 | 0 | 300 |
| 125.14 | 2999 | 59.9 | 0 | 0 | 300 |
| 130.15 | 3000 | 59.9 | 0 | 0 | 300 |
| 135.16 | 3000 | 59.9 | 0 | 0 | 300 |
| 140.16 | 3000 | 59.9 | 0 | 0 | 300 |
| 145.17 | 3000 | 59.9 | 0 | 0 | 300 |
| 150.17 | 3000 | 59.9 | 0 | 0 | 300 |
| 155.18 | 3000 | 59.9 | 0 | 0 | 300 |
| 160.18 | 2999 | 59.9 | 0 | 0 | 300 |
| 165.19 | 3000 | 59.9 | 0 | 0 | 300 |
| 170.20 | 2730 | 59.9 | 0 | 0 | 300 |
| 175.20 | 6000 | 59.9 | 0 | 0 | 300 |
| 180.21 | 6000 | 59.9 | 0 | 0 | 300 |
| 185.21 | 5997 | 59.9 | 0 | 0 | 300 |
| 190.22 | 5998 | 59.9 | 0 | 0 | 300 |
| 195.23 | 5999 | 59.9 | 0 | 0 | 300 |
| 200.23 | 5999 | 59.9 | 0 | 0 | 300 |
| 205.24 | 5998 | 59.9 | 0 | 0 | 300 |
| 210.24 | 6000 | 59.9 | 0 | 0 | 300 |
| 215.25 | 6000 | 59.9 | 0 | 0 | 300 |
| 220.25 | 5999 | 59.9 | 0 | 0 | 300 |
| 225.26 | 5999 | 59.9 | 0 | 0 | 300 |
| 230.27 | 5587 | 59.9 | 0 | 0 | 300 |
| 235.27 | 10000 | 59.9 | 0 | 0 | 300 |
| 240.28 | 9999 | 59.9 | 0 | 0 | 300 |
| 245.28 | 10000 | 59.9 | 0 | 0 | 300 |
| 250.29 | 9997 | 59.9 | 0 | 0 | 300 |
| 255.29 | 10000 | 59.9 | 0 | 0 | 300 |
| 260.30 | 9997 | 59.9 | 0 | 0 | 300 |
| 265.31 | 9998 | 59.9 | 0 | 0 | 300 |
| 270.31 | 10000 | 59.9 | 0 | 0 | 300 |
| 275.32 | 10000 | 59.7 | 0 | 0 | 299 |
