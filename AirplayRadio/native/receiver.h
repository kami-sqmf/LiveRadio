#pragma once
#include <stdint.h>
#ifdef AR_IMPORT
#define AR_API extern "C" __declspec(dllimport)
#else
#define AR_API extern "C" __declspec(dllexport)
#endif
// All calls except read/stats are serialized by the managed lifecycle owner.
// Destroy only after readers have released their handle. No managed callbacks.
AR_API void* ar_create();
AR_API int ar_start(void* handle, const char* name, const char* device_id, const char* key_path);
AR_API void ar_destroy(void* handle);
AR_API int ar_read(void* handle, float* output, int samples);
AR_API void ar_clear(void* handle);
AR_API int ar_text(void* handle, int field, char* output, int capacity);
AR_API int ar_port(void* handle);
AR_API uint64_t ar_decoded_samples(void* handle);
AR_API uint64_t ar_dropped_samples(void* handle);
// 0 queued samples, 1 underruns, 2 ring contention, 3 decoder failures,
// 4 RTP packets requested again (includes repeated requests), 5 max input gap ms.
AR_API uint64_t ar_stat(void* handle, int field);
AR_API uint64_t ar_cover_revision(void* handle);
// Returns size with null output, -1 if the expected revision is stale. Max 2 MiB.
AR_API int ar_cover(void* handle, uint64_t revision, unsigned char* output, int capacity);
