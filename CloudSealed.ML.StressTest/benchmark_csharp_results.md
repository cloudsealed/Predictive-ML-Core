
# Relatório de Teste de Stress: Zero-Alloc Risk Probe (C# SIMD)
- **Iterações (Amostras Processadas):** 10,000,000
- **Tamanho do Buffer:** 1024 floats
- **Tempo Total:** 2.4171 segundos
- **Throughput:** 4,137,111.14 avaliações/segundo
- **Memória Alocada no GC (Heap):** 40 bytes

**Veredito:** O motor SIMD AVX2 escrito nativamente atinge 40 alocações no Garbage Collector, e consegue fazer milhões de cálculos por segundo, sendo ideal para APIs de missão crítica em C#.
