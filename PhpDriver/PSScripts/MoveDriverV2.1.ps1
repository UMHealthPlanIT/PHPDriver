$destinationFolder = "C:\Program Files\Domain\Driver.old"
Try {
    Remove-Item "C:\Program Files\Domain\Driver.old" -Recurse -ErrorAction Stop
    Move-Item -Path "C:\Program Files\Domain\Driver\*" -Destination $destinationFolder -ErrorAction Stop
}
Catch {
    $newFolder = "Driver.$((Get-Date).ToString("ddMMyyhhmmss"))"
    New-Item -Path "C:\Program Files\Domain\" -Name $newFolder -ItemType Directory
    $destinationFolder = "C:\Program Files\Domain\$newFolder"
    Move-Item -Path "C:\Program Files\Domain\Driver\*" -Destination $destinationFolder
}