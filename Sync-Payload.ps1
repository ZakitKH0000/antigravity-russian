$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Web.Extensions
$serializer=New-Object System.Web.Script.Serialization.JavaScriptSerializer
$serializer.MaxJsonLength=64000000
$dictionary=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'payload\dictionary.json'))
$data=$serializer.DeserializeObject($dictionary)
if($data -isnot [Collections.Generic.Dictionary[string,object]] -or $data.get_Count() -eq 0) { throw 'Dictionary must be a nonempty JSON object.' }
foreach($pair in $data.GetEnumerator()) { if($pair.Value -isnot [string]) { throw 'Dictionary values must be strings.' } }
$template=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'payload\localize-ui.template.js'))
$marker='/* RU_DICTIONARY */'
if($template.IndexOf($marker) -lt 0 -or $template.IndexOf($marker) -ne $template.LastIndexOf($marker)) { throw 'Invalid dictionary marker in overlay template.' }
$chatJson=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'payload\chat-labels.json'))
$chatLabels=$serializer.DeserializeObject($chatJson)
if($chatLabels -isnot [object[]] -or $chatLabels.Length -eq 0) { throw 'Chat labels must be a nonempty JSON array.' }
foreach($label in $chatLabels) { if($label -isnot [string] -or -not $data.ContainsKey($label)) { throw ('Chat label is missing from the dictionary: '+$label) } }
$chatMarker='/* RU_CHAT_LABELS */'
if($template.IndexOf($chatMarker) -lt 0 -or $template.IndexOf($chatMarker) -ne $template.LastIndexOf($chatMarker)) { throw 'Invalid chat label marker in overlay template.' }
$overlay=$template.Replace($marker,$dictionary.Trim()).Replace($chatMarker,$chatJson.Trim())
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'payload\localize-ui.js'),$overlay,(New-Object Text.UTF8Encoding($false)))
Write-Output ('Dictionary synchronized: '+$data.get_Count()+' entries')
