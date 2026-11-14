# What Inventor is running right now, with the date it started, and what each
# instance has open. Used to tell a fresh test instance from the user's.
$out = 'C:\Temp\inventor-now.txt'
"inventor now: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" | Out-File $out -Encoding utf8

Get-Process Inventor -ErrorAction SilentlyContinue | ForEach-Object {
    $p = $_
    "$($p.Id)`tstarted $($p.StartTime.ToString('yyyy-MM-dd HH:mm:ss'))`tthreads $($p.Threads.Count)`tresponding $($p.Responding)`t$($p.MainWindowTitle)" |
        Out-File $out -Append -Encoding utf8
}

"--- inventor tasks ---" | Out-File $out -Append -Encoding utf8
schtasks /query /tn OpenThread /v /fo list 2>&1 | Select-String 'Status|Last Run|Last Result' |
    ForEach-Object { $_.Line.Trim() } | Out-File $out -Append -Encoding utf8

"--- recent inventor exe launches ---" | Out-File $out -Append -Encoding utf8
Get-WinEvent -FilterHashtable @{ LogName = 'Application'; Id = 1000 } -MaxEvents 5 -ErrorAction SilentlyContinue |
    ForEach-Object { $_.TimeCreated.ToString('yyyy-MM-dd HH:mm:ss') + ' ' + ($_.Message -split "`n")[0] } |
    Out-File $out -Append -Encoding utf8
exit 0
