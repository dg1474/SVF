#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/../../.." && pwd)"

echo "=================================================="
echo " Running SVF.NET Tests in Linux Native Environment"
echo " Project Root: ${PROJECT_ROOT}"
echo "=================================================="

# Check dotnet installation
if ! command -v dotnet &> /dev/null; then
    echo "ERROR: dotnet CLI is not installed or not in PATH."
    exit 1
fi

echo "Active .NET version: $(dotnet --version)"

# Setup mock SVF tools if native build not present
if [ ! -f "${PROJECT_ROOT}/Release-build/bin/wpa" ]; then
    echo "Notice: Native wpa not found. Staging mock wpa for analysis validation..."
    export SVF_DIR="/tmp/mock_svf_linux"
    mkdir -p "${SVF_DIR}/bin"
    cat << 'EOF' > "${SVF_DIR}/bin/wpa"
#!/bin/sh
echo "*********General Stats***************"
echo "AddrsNum 42"
echo "CallsNum 1"
echo "IndCallSites 1"
echo "CallSite: call void fp() -> { target1, target2 }"
exit 0
EOF
    chmod +x "${SVF_DIR}/bin/wpa"
    touch /tmp/mock_test.bc
    TEST_BC="/tmp/mock_test.bc"
else
    TEST_BC="${PROJECT_ROOT}/Test-Suite/test_cases_bc/basic_c_tests/struct-nested-array3.c.bc"
fi

# Run test suite
dotnet run --project "${SCRIPT_DIR}/SVF.NET.Tests.csproj" -c Release -- "${TEST_BC}"

echo "Linux test suite execution finished."
