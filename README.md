# Network Troubleshooter (.NET MAUI)

This is a cross-platform GUI application built with .NET MAUI for basic network and DNS troubleshooting.
It allows users without administrative privileges to perform common diagnostic tasks.

## Features

*   **Local Info:** Displays local network interface configuration (IP addresses, MAC, Gateways, DNS Servers).
*   **DNS Lookup:** Performs various DNS record lookups (A, AAAA, MX, NS, CNAME, PTR, SOA, TXT) using system default or specified DNS servers.
*   **Ping:** Sends ICMP echo requests to a target host or IP.
*   **Traceroute:** Traces the network path to a target host or IP.
*   **Port Scan:** Checks the status (Open, Closed, Filtered) of specified TCP ports on a target host.
*   **HTTP Check:** Performs a basic HTTP/HTTPS GET request to a URL and displays the status code.

## Target Platforms

*   Windows
*   macOS

## Prerequisites

*   .NET 8 SDK (or later) with the MAUI workload installed (`dotnet workload install maui`).

## Building and Running

1.  **Clone the repository:**
    ```bash
    git clone <repository-url>
    cd NetworkTroubleshooter
    ```
2.  **Restore dependencies:**
    ```bash
    dotnet restore
    ```
3.  **Build and Run for a specific platform:**
    *   **Windows:** 
        ```powershell
        # Find the correct TargetFramework in NetworkTroubleshooter.csproj (e.g., net8.0-windows10.0.19041.0)
        dotnet build -t:Run -f netX.Y-windowsZ.A.B.C 
        ```
    *   **macOS:**
        ```bash
        # Find the correct TargetFramework in NetworkTroubleshooter.csproj (e.g., net8.0-maccatalyst)
        dotnet build -t:Run -f netX.Y-maccatalyst
        ```
    *   *(Replace `netX.Y...` with the actual TargetFramework from your `.csproj` file)*

## Publishing Self-Contained Executables

Refer to the GitHub Actions workflow (`.github/workflows/dotnet-release.yml`) or use the `dotnet publish` command manually as described in the initial requirements (Step 10).

## Documentation

*   Architectural decisions and prompts are stored in the `Context/prompt.txt` file (excluded via `.gitignore`).
*   Additional documentation can be placed in the `/documents` folder. 