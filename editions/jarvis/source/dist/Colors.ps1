Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$path = Join-Path $PSScriptRoot 'assets\colors.json'
$palette = @{ accent='#a93f68'; accent2='#ce8fa3'; background='#050409'; text='#ece2e7' }
if (Test-Path $path) {
    $saved = Get-Content $path -Raw | ConvertFrom-Json
    foreach ($key in @('accent','accent2','background','text')) {
        if ($saved.$key -match '^#[0-9a-fA-F]{6}$') { $palette[$key] = $saved.$key }
    }
}
$form = New-Object Windows.Forms.Form
$form.Text = 'HUD Live colors'
$form.ClientSize = New-Object Drawing.Size(370,280)
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedDialog'
$form.MaximizeBox = $false
$keys = @('accent','accent2','background','text')
$labels = @('Primary accent','Secondary accent','Background','Text')
for ($i=0; $i -lt $keys.Count; $i++) {
    $label = New-Object Windows.Forms.Label
    $label.Text=$labels[$i]; $label.Location=New-Object Drawing.Point(20,(22+$i*45)); $label.AutoSize=$true
    $form.Controls.Add($label)
    $button=New-Object Windows.Forms.Button
    $button.Tag=$keys[$i]; $button.Text=$palette[$keys[$i]]
    $button.Location=New-Object Drawing.Point(175,(15+$i*45)); $button.Size=New-Object Drawing.Size(170,32)
    $button.BackColor=[Drawing.ColorTranslator]::FromHtml($button.Text)
    $button.Add_Click({
        $dialog=New-Object Windows.Forms.ColorDialog
        $dialog.FullOpen=$true; $dialog.Color=[Drawing.ColorTranslator]::FromHtml($this.Text)
        if ($dialog.ShowDialog() -eq 'OK') {
            $hex='#{0:X2}{1:X2}{2:X2}' -f $dialog.Color.R,$dialog.Color.G,$dialog.Color.B
            $palette[$this.Tag]=$hex; $this.Text=$hex; $this.BackColor=$dialog.Color
        }
        $dialog.Dispose()
    })
    $form.Controls.Add($button)
}
$save=New-Object Windows.Forms.Button
$save.Text='Save and apply'; $save.Location=New-Object Drawing.Point(175,210); $save.Size=New-Object Drawing.Size(170,36)
$save.Add_Click({
    $json=ConvertTo-Json -InputObject $palette -Compress
    [IO.File]::WriteAllText($path,$json)
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'assets\colors.js'),"window.HUD_COLORS = $json;")
    & (Join-Path $PSScriptRoot 'Manage.ps1') -Action Stop
    Start-Sleep -Milliseconds 500
    Start-Process -FilePath (Join-Path $PSScriptRoot 'Start Wallpaper.cmd') -WindowStyle Hidden
    $form.Close()
})
$form.Controls.Add($save)
$form.ShowDialog() | Out-Null
