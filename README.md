# ErrorTracker
A light-weight event/error tracking service written in C#

## Server Installation
ErrorTrackerServer is a Windows Service with a built-in service manager GUI.

1) Download a server release from the [releases page](https://github.com/bp2008/ErrorTracker/releases) and extract to a folder of your choosing.
2) Run ErrorTrackerServer.exe

![Screenshot](https://i.imgur.com/jtrAKDf.png)

3) Press "Install Service", then "Start Service".  

A copy of the `Settings.cfg` file will be written in the program's data folder.

By default, the embedded web server listens on port `80` (HTTP) and `443` (HTTPS).  If you are using these ports for other services, stop the service, click the "Open Data Folder" button, and edit `Settings.cfg` to change the port numbers.  To disable the HTTP or HTTPS interfaces, assign a port number of `-1`.

The configuration file also contains a number of other settings as well as your User List and Project List, but it is recommended not to edit these values directly.  The **web interface** provides safer access to the configuration.

4) With the service running, access the **web interface** by connecting to your local machine on the specified HTTP port.  E.g. http://127.0.0.1/

5) Log in with user name `admin` and password `admin`.

6) You should now be viewing the "Client Home" page, where you may **change the default admin password** to something more secure.

7) To edit the server configuration and manage users and projects, choose the "Admin" menu item at the top. See [Admin Settings Documentation](https://github.com/bp2008/ErrorTracker/wiki/Admin-Settings-Documentation) for more information about ErrorTracker Settings.

## Client Installation
`ErrorTrackerClient.dll` is a light-weight library built on .NET Framework 4.5.2 which makes it easy to submit events to ErrorTracker.

1) Download a client release from from the [releases page](https://github.com/bp2008/ErrorTracker/releases) and include `ErrorTrackerClient.dll` as a dependency in your .NET project.

2) Somewhere in your .NET project, create a static instance of the `ErrorClient` class.

```cs
private static ErrorClient client = new ErrorClient(
    JsonConvert.SerializeObject, 
    () => "https://127.0.0.1/Submit?p=MyProject&k=f90ez8feZSFe90sifesLJszfE8", 
    () => "C:\\ErrorTrackerTemp\\"
);
```
The constructor takes 3 arguments.

The first argument should be a reference to the `JsonConvert.SerializeObject` method from JSON.NET.  `ErrorTrackerClient.dll` wasn't built with a JSON.NET dependency, so you can supply your own version without a version conflict.

The second argument should be a function (or lambda expression) which returns the event submission URL for your project (see **Your First Project** section below).

The third argument should be a function (or lambda expression) which returns the path to a directory on a local hard drive where the client can save events temporarily if the ErrorTracker server is unreachable.

3) Submit an Event

```cs
try
{
  int.Parse("Not an integer");
}
catch (Exception ex)
{
  Event ev = new Event(EventType.Error, "Unhandled Exception", ex.ToString());
  ev.SetTag("Machine Name", Environment.MachineName);
  
  // To submit on the current thread, just call: client.SubmitEvent(ev);
  // As an example, we'll submit from a background thread using .NET's built-in thread pool.
  System.Threading.ThreadPool.QueueUserWorkItem((arg) => { client.SubmitEvent(ev); });
}
```

## Your First Project

From the Admin > Projects page of the web interface, click "Add Project".  Choose the name wisely; if you decide to rename the project later you'll need to manually rename it in the configuration file and also rename the project's database file to match.

![screenshot](https://i.imgur.com/orxxMl0.png)

Once you've added a project, you may copy its Submission URL into your .NET application and feed it into the `ErrorClient` class constructor.

If you click your project in the Projects table, a dialog appears with options.

![screenshot](https://i.imgur.com/lIPc8PR.png)

* **Replace Submission Key** generates a new submission key, invalidating the old one in case you accidentally leak it.
* **Copy Submission URL** copies the submission URL to the clipboard.
* **Max Event Age**, if greater than 0, sets the number of days before events are automatically deleted to save space.

## Building From Source

This project was built with Visual Studio 2019 Community Edition and node.js v12.14.1.  You will also need to download my utility library, [bp2008/BPUtil](https://github.com/bp2008/BPUtil), and repair the dependency in the ErrorTrackerServer project.  BPUtil is updated separately and may sometimes be slightly incompatible. Sorry about that.
