# Export API Helper library

This library is intended to help developers using Relativity's Object Manager Export API by hiding the concurrency needed to achieve maximum throughput behind an easier to use API.

## Getting Started

The simplest way to use this library is to use the public NuGet package [`Relativity.ObjectManager.ExportApiHelper`](https://www.nuget.org/packages/Relativity.ObjectManager.ExportApiHelper). 
Alternatively, this source code can be used by leveraging the included Visual Studio 2015 Solution and Projects. 

### Prerequisites

The only external dependency is on the [`Relativity.ObjectManager`](https://www.nuget.org/packages/Relativity.ObjectManager) NuGet package. 

## Usage

A full, working example is available in the `ExportApiHelperSample` project but in short using this library
requires two pieces of work: writing Export API Helper initialization and execution code,
and writing an Export API Helper handler. 

### Initialize and execute

The code below configures the export by initializing an `ExportApiHelperConfig` object then uses it to create an `ExportApiHelper` object. The `ExportApiHelper` object's `Run` method is then called,  
providing it a "handler" that implements `IExportApiHandler`. The handler's methods receive various events, including an `Item` event which provides the exported data. 

```C#
ExportApiHelperConfig config = new ExportApiHelperConfig()
	{
	BlockSize = 1000,
	QueryRequest = new QueryRequest()
		{
		Fields = new FieldRef[]
			{
			new FieldRef {Name = "Control Number"},
			new FieldRef {Name = "Extracted Text"}
			},
		MaxCharactersForLongTextValues = 1024 * 100
		},
	WorkspaceId = 1234567,
	RelativityUrl = new Uri("https://relativity.mycompany.com"),
	Credentials = new UsernamePasswordCredentials("me@mycompany.com", "MyPasswordDontDoThis987$")
	};

ExportApiHelper.ExportApiHelper helper = config.Create();

helper.Run(new ExportApiHandler());
```

### Handler

The handler below simply prints information about the documents 

```C#
class ExportApiHandler : IExportApiHandler
{
	private int _count = 0;

	public ExportApiHandler()
	{
	}

	public void Before(ExportInitializationResults results) {
		Console.WriteLine("ExportApiHandler Before called with itemCount: "+results.RecordCount);
	}

	public bool Item(RelativityObjectSlim item)
	{
		int currentCount = Interlocked.Increment(ref _count);

		if (currentCount % 1000 == 0)
		{
			Console.WriteLine(currentCount);
		}

		if (item.Values[1] is string)
		{
			Console.WriteLine("ExportApiHandler Item " + _count + " called with control of '" + item.Values[0].ToString() + "' and text type of String");
		}
		else if (item.Values[1] is Stream)
		{
			Console.WriteLine("ExportApiHandler Item " + _count + " called with control of '" + item.Values[0].ToString() + "' and text type of Stream");
		}
		else
		{
			Console.WriteLine("ExportApiHandler Item " + _count + " called with control of '" + item.Values[0].ToString() + "' and text type of Error");
			return false;
		}

		return true;
	}

	public void After(bool complete)
	{
		Console.WriteLine("ExportApiHandler After called with complete = " + complete);
	}

	public void Error(string message, Exception exception)
	{
		Console.WriteLine("ExportApiHandler Error called with message '" + message + "' and exception '" + exception?.Message + "'");
	}

	public bool TheadSafe => true;
}
```

## Versioning

We use [SemVer](http://semver.org/). 

## License

This project is licensed by Relativity ODA LLC under this [LICENSE](LICENSE) 


