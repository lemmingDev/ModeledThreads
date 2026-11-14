# Reports every Inventor process: window title, whether its window responds,
# and its threads' wait states, so a wedged instance can be told from a busy one.
$out = 'C:\Temp\inventor-state.txt'
"inventor processes" | Out-File $out -Encoding utf8

Get-Process Inventor -ErrorAction SilentlyContinue | ForEach-Object {
    $p = $_
    "$($p.Id)`tresponding=$($p.Responding)`tthreads=$($p.Threads.Count)`tstarted=$($p.StartTime.ToString('HH:mm:ss'))`t$($p.MainWindowTitle)" |
        Out-File $out -Append -Encoding utf8
    $p.Threads | Where-Object { $_.ThreadState -ne 'Wait' } | Select-Object -First 8 | ForEach-Object {
        "    thread $($_.Id) $($_.ThreadState) $($_.WaitReason)" | Out-File $out -Append -Encoding utf8
    }
}

"--- harness ---" | Out-File $out -Append -Encoding utf8
Get-Process ThreadHarness -ErrorAction SilentlyContinue | ForEach-Object {
    $p = $_
    "$($p.Id)`tresponding=$($p.Responding)`tstarted=$($p.StartTime.ToString('HH:mm:ss'))" |
        Out-File $out -Append -Encoding utf8
    $p.Threads | Where-Object { $_.ThreadState -ne 'Wait' } | Select-Object -First 8 | ForEach-Object {
        "    thread $($_.Id) $($_.ThreadState) $($_.WaitReason)" | Out-File $out -Append -Encoding utf8
    }
}
exit 0
