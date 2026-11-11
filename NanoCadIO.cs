using System;
using System.Collections.Generic;
using Teigha.Runtime;
using HostMgd.ApplicationServices;
using Teigha.DatabaseServices;
using HostMgd.EditorInput;


namespace NanoCadIO
{
	public class LayoutCommands
	{
		[CommandMethod("LISTLAYOUTS")]
		public void ListAllLayouts() {
			Document doc = Application.DocumentManager.MdiActiveDocument;
			Editor ed = doc.Editor;
			
			ed.WriteMessage($"\nЛисты: {String.Join(", ", GetLayoutNames())}");
		}
		private List<string> GetLayoutNames() {
			List<string> layoutNames = new List<string>();
			
			Database database = Application.DocumentManager.MdiActiveDocument.Database;
			
			using (Transaction transaction = database.TransactionManager.StartTransaction())
			{
				DBDictionary layoutDict = (DBDictionary)transaction.GetObject(
					database.LayoutDictionaryId, 
					OpenMode.ForRead
				);
				
				foreach (DBDictionaryEntry entry in layoutDict)
				{
					Layout layout = (Layout)transaction.GetObject(
						entry.Value,
						OpenMode.ForRead
					);
					layoutNames.Add(layout.LayoutName);
				}
			}
			
			return layoutNames;
		}
	}
}
