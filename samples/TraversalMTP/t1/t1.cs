using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize]
namespace n1;

[TestClass]
public class t1
{
	[TestMethod]
	public void m1() { System.Threading.Thread.Sleep(5000); }
}