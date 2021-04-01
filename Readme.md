# GPX Plugin for ArcGIS

An extension (plugin) for ArcGIS 10.x to allow ArcCatalog
and ArcMap to open a GPX file as a native GIS dataset.
The GPX file does not need to be converted to a more
common data format before viewing.

## Build

The plugin was developed with C#, and .Net 3.5.
It requires the ArcObjects libraries from ESRI.
Visual Studio 2010 project files are provided for building the source code.

### Testing

After building a debug version with Visual Studio

1) Copy `reg.bat` and `unreg.bat` to the `10xCode/bin/debug` folder.
2) Run `reg.bat` as administrator
3) Start a debug build and visual studio should launch ArcCatalog
   after building the debug dll
4) Use ArcCatalog to test browsing/viewing a GPX file.
5) You do not need to re-register between builds unless you change
   the GUID

## Deploy

The plugin requires installation by an administrator.

1) Copy `GpxPlugin.dll` from `10xCode/bin/release` to
   some stable system folder.
2) Copy `reg.bat` and `unreg.bat` to the same folder.
3) Run `reg.bat` as administrator.
4) Use ArcMap and/or ArcCatalog as usual.
5) To uninstall:
   - Run `unreg.dll`
   - Delete `reg.bat`, `unreg.bat`, and `GpxPlugin.dll`

See the MS Word file in the `Docs` folder for more information.

## Publish

New versions (or recompiles for new versions or ArcObjects)
should be published to
[IRMA](https://irma.nps.gov/DataStore/Reference/Profile/2203303)
and the PDS (Alaska Region's GIS network drive).

1) Copy `reg.bat`, `unreg.bat`, and `GpxPlugin.dll` to a
   folder named for the version of ArcGIS that it was compiled
   against (i.e. `GpxPlugin10.7`)
2) Zip up the folder.
3) Provide the zip file to the PDS Manager for addition to the
  `X:\GIS\Apps\GpxPlugin` folder.
4) Login to IRMA and add the zip file to the Download list.
   (You need to be the IRMA project owner)

## Using

A GPX file is a compound data set.  It expands to 1 or more
spatial feature classes.  One is a point feature class for
all way points in the GPX file. Another is a polyline feature
class for all track logs.  The third is a polygon, which is
just a closed versions of the track logs.

Preview the feature classes in a GPX file as a table or a
spatial view in ArcCatalog. Use the `Add Data` tool in
ArcMap to add a GPX file to a map.

See the MS Word file in the `Docs` folder for more information.
