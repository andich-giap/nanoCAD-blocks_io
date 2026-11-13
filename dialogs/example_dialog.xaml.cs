using System.Windows;
using Teigha.DatabaseServices;
using HostMgd.ApplicationServices;


namespace ExampleDialog
{
	public partial class PaletteWpfChild : System.Windows.Controls.UserControl
	{
		public PaletteWpfChild()
		{
			InitializeComponent();
			this.Width = 300;
		}
		private void UpdateBtn_Click(object sender, RoutedEventArgs e)
		{
			NumEntsLabel.Content = CalculateObjects().ToString();
		}
		private int CalculateObjects()
		{
			Document doc = HostMgd.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
			Database db = doc.Database;

			int curSpaceObjects = 0;

			using (Transaction transaction = db.TransactionManager.StartTransaction())
			{
				if (transaction.GetObject(db.CurrentSpaceId, OpenMode.ForRead) is not BlockTableRecord blockTableRecord)
					return 0;
				
				foreach (ObjectId obj in blockTableRecord)
				{
					curSpaceObjects++;
				}
			}
			return curSpaceObjects;
		}
	}
}
