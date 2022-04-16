# IntelliSenseLocalizer
a tool for generate Localized IntelliSense files. 用于生成本地化IntelliSense文件的工具。

## How to use

### 1. install the tool
```shell
dotnet tool install -g islocalizer --prerelease
```

### 2. get the local Localized IntelliSense files

There has two options:

#### 1. load from github (if there has target file)
```shell
islocalizer load github
```

#### 2. build file yourself
```shell
islocalizer build
```
This command may take a whole day... But when cached all page it will be completed faster.

### 3. install builded file
```shell
islocalizer install
```

#### run `islocalizer -h` to see more command.