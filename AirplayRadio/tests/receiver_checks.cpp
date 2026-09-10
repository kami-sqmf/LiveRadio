#define AR_IMPORT
#include "../native/receiver.h"
#include <cstdio>
#include <cstring>
#include <vector>
#include <winsock2.h>
#include <ws2tcpip.h>
int main() {
    for (int cycle = 0; cycle < 3; ++cycle) {
        void* r = ar_create(); if (!r) return 1;
        std::vector<float> silence(1024, 1);
        if (ar_read(r, silence.data(), silence.size()) != 0) return 2;
        for (float f : silence) if (f != 0) return 3;
        int result = ar_start(r, "Airplay Radio Test", "02:41:52:00:00:01", "");
        if (result) { printf("start failed %d\n", result); ar_destroy(r); return 4; }
        SOCKET s = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
        DWORD timeout = 3000; setsockopt(s, SOL_SOCKET, SO_RCVTIMEO, (const char*)&timeout, sizeof(timeout));
        sockaddr_in addr{}; addr.sin_family = AF_INET; addr.sin_port = htons(ar_port(r)); inet_pton(AF_INET, "127.0.0.1", &addr.sin_addr);
        if (connect(s, (sockaddr*)&addr, sizeof(addr))) return 5;
        const char request[] = "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nDACP-ID: ABCD\r\nActive-Remote: 1234\r\n\r\n";
        send(s, request, strlen(request), 0);
        char response[4096]{}; int n = recv(s, response, sizeof(response)-1, 0);
        if (n <= 0 || !strstr(response, "200 OK")) { printf("Bad RTSP response: %s\n", response); return 6; }
        char remote[256]{}; ar_text(r, 4, remote, sizeof(remote));
        if (!strstr(remote, "\n127.0.0.1\nABCD\n1234")) return 7;
        auto revision = ar_cover_revision(r);
        const char clear[] = "SET_PARAMETER * RTSP/1.0\r\nCSeq: 2\r\nContent-Type: image/none\r\nContent-Length: 0\r\n\r\n";
        send(s, clear, strlen(clear), 0);
        memset(response, 0, sizeof(response)); n = recv(s, response, sizeof(response)-1, 0);
        if (n <= 0 || !strstr(response, "200 OK") || ar_cover_revision(r) <= revision || ar_cover(r, ar_cover_revision(r), nullptr, 0) != 0) return 8;
        closesocket(s); ar_destroy(r);
    }
    puts("PASS: native DLL create/read-silence, RTSP OPTIONS/image-none and three start/stop cycles");
}
