# docker web terminal based on .net core



Usage:

- Config docker daemon to allow remote access

- Start a container to be used in this test, make sure `/bin/bash` works in the container.

- Config `Startup.cs` according to your config

  ```c#
  const string id = "5fcd5d77e072";
  
  var client = new DockerClientConfiguration(new Uri("http://233.233.233.233:2375")).CreateClient();
  ```

- execute following command:

  ```bash
  $ dotnet restore
  
  $ dotnet run
  ```

- visit http://localhost:5000 in your broswer, you shall see this:

  <img src="demo.png">

# 使用dotnetcore的docker web terminal示例

用法:

- Config docker daemon to allow remote access

- Start a container to be used in this test, make sure `/bin/bash` works in the container.

- Config `Startup.cs` according to your config

  ```c#
  const string id = "5fcd5d77e072";
  
  var client = new DockerClientConfiguration(new Uri("http://233.233.233.233:2375")).CreateClient();
  ```

- execute following command:

  ```bash
  $ dotnet restore
  
  $ dotnet run
  ```

- visit http://localhost:5000 in your broswer, you shall see this:

  <img src="/Users/likun/code/dotnet/docker-web-terminal-dotnet-core/demo.png">