# Fixtures sintéticos

Los archivos son pequeños, legibles y están diseñados para inspección visual y `--trace`:

1. `01-rice-4k.osu`: rice puro.
2. `02-isolated-ln-4k.osu`: LN aislada.
3. `03-full-ln-like-4k.osu`: heads distintos con release común.
4. `04-release-chord-4k.osu`: release chord.
5. `05-release-staircase-4k.osu`: escalera de releases.
6. `06-alternating-durations-4k.osu`: duraciones 1 / 0.5 beat.
7. `07-saturated-4k.osu`: geometría sin espacio.
8. `08-crossing-range-4k.osu`: LN externa que atraviesa un rango sugerido de 1000–1500 ms.
9. `09-basic-4k.osu`: mezcla 4K.
10. `10-basic-7k.osu`: mezcla 7K.
11. `11-high-keymode-18k.osu`: validación de keymode alto.

Todos usan 120 BPM (`500 ms = 1 beat`) para que los cálculos sean transparentes.
