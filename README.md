# YmapSorter V0.1
YMAP Entity Sorter is a simple yet powerful tool designed to clean up your CodeWalker map files. It parses your map data and automatically reorganizes all entities, grouping them alphabetically by their archetypeName.

Why use this tool? When creating complex maps, entities are often scattered in a random order within the file. This can cause significant issues when generating _manifest.ymf files, as some generators fail to register props correctly if they are not grouped by type.

By sorting your XML with this tool, you ensure a cleaner structure and prevent missing props in your generated manifests.

How to use (V1):

Open your .ymap in CodeWalker.

Go to File > Export XML.

Open this tool and select your exported XML file.

The sorted file will be saved in a new export folder.

Import the new XML back into CodeWalker or your server.

⚠️ VERSION 1.0 NOTE: Currently, this tool strictly supports CodeWalker XML files. You must export your map to XML format before using the sorter.

🚀 COMING SOON (V2): The next version is currently in development and will support direct binary .ymap editing, allowing you to drag and drop game files without needing to convert them to XML first.
