# Tiny loopback HTTP server that mimics https://opencode.ai/zen/go/v1/usage
# Used to test the app without touching the real endpoint.
param(
    [int]$Port = 18771,
    [ValidateSet('normal', 'real', 'unauthorized', 'forbidden', 'malformed', 'servererror', 'slow', 'sparse')]
    [string]$Case = 'normal',
    [int]$DelayMs = 250
)

$ErrorActionPreference = 'Stop'

function Iso([datetime]$dt) {
    $dt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")
}

$now = Get-Date
$normalBody = @"
{"usage":{
  "rolling":{"status":"ok","percent":12.5,"resetsAt":"$(Iso $now.AddHours(4).AddMinutes(10))"},
  "weekly":{"status":"ok","percent":78,"resetsAt":"$(Iso $now.AddDays(2).AddHours(5))"},
  "monthly":{"status":"ok","percent":94,"resetsAt":"$(Iso $now.AddDays(12).AddHours(3))"}},
 "plan":"go","workspace":"Personal"}
"@

$sparseBody = @"
{"usage":{
  "rolling":{"status":"ok","percent":45},
  "weekly":{"status":"exhausted","percent":100,"resetsAt":"$(Iso $now.AddDays(1))"},
  "monthly":{"used":3.25,"limit":12}}}
"@

$unauthorizedBody = '{"error":"unauthorized","message":"Invalid API key"}'
$forbiddenBody = '{"error":"EntitlementError","message":"This workspace has no OpenCode Go subscription"}'
$realBody = '{"usage":{"rolling":{"status":"ok","percent":0,"resetsAt":"2026-10-21T09:15:23.000Z"},"weekly":{"status":"ok","percent":0,"resetsAt":"2026-10-26T00:00:00.000Z"},"monthly":{"status":"ok","percent":14,"resetsAt":"2026-11-14T01:49:31.000Z"}}}'
$malformedBody = '{"hello":"world","nested":{"a":1}}'
$serverErrorBody = '{"error":"internal","message":"upstream unavailable"}'

$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
$listener.Start()
Write-Host ("mock server 127.0.0.1:{0} case={1}" -f $Port, $Case)

try {
    while ($true) {
        $client = $listener.AcceptTcpClient()
        try {
            $stream = $client.GetStream()
            $stream.ReadTimeout = 5000
            $buffer = New-Object byte[] 8192
            $sb = [System.Text.StringBuilder]::new()
            while ($stream.DataAvailable -or $sb.Length -eq 0) {
                $n = $stream.Read($buffer, 0, $buffer.Length)
                if ($n -le 0) { break }
                [void]$sb.Append([System.Text.Encoding]::ASCII.GetString($buffer, 0, $n))
                if ($sb.ToString().Contains("`r`n`r`n")) { break }
            }
            $request = $sb.ToString()
            $requestLine = ($request -split "`r`n")[0]
            $hasAuth = $request -match '(?im)^Authorization:\s*Bearer\s+\S+'
            Write-Host ("REQ {0} auth={1}" -f $requestLine, $hasAuth)

            $code = 200
            $reason = 'OK'
            $body = $normalBody

            switch ($Case) {
                'normal' { $body = $normalBody }
                'sparse' { $body = $sparseBody }
                'real' { $body = $realBody }
                'unauthorized' { $code = 401; $reason = 'Unauthorized'; $body = $unauthorizedBody }
                'forbidden' { $code = 403; $reason = 'Forbidden'; $body = $forbiddenBody }
                'malformed' { $body = $malformedBody }
                'servererror' { $code = 500; $reason = 'Internal Server Error'; $body = $serverErrorBody }
                'slow' { Start-Sleep -Milliseconds $DelayMs; $body = $normalBody }
            }

            if (-not $hasAuth -and $Case -eq 'normal') {
                $code = 401
                $reason = 'Unauthorized'
                $body = $unauthorizedBody
            }

            $bytes = [System.Text.Encoding]::UTF8.GetBytes($body.Trim())
            $head = "HTTP/1.1 $code $reason`r`n" +
                    "Content-Type: application/json`r`n" +
                    "Content-Length: $($bytes.Length)`r`n" +
                    "Connection: close`r`n`r`n"
            try {
                $headBytes = [System.Text.Encoding]::ASCII.GetBytes($head)
                $stream.Write($headBytes, 0, $headBytes.Length)
                $stream.Write($bytes, 0, $bytes.Length)
                $stream.Flush()
            }
            catch {
                Write-Host ("client pergi lebih dulu: {0}" -f $_.Exception.Message)
            }
            Start-Sleep -Milliseconds 40
        }
        finally {
            $client.Close()
        }
    }
}
finally {
    $listener.Stop()
}
