# IntelliSenseLocalizer
a tool for generate Localized IntelliSense files. 用于生成本地化IntelliSense文件的工具。

## How to use

### 1. install the tool
```shell
dotnet tool install -g islocalizer --prerelease
```

### 2. try install Localized IntelliSense files from github

This command try get the Localized IntelliSense files from github what moniker is `net6.0` and locale is `zh-cn`. And install it:

```shell
islocalizer install auto -m net6.0 -l zh-cn
```
Also you can set the ContentCompareType by `-cc`
```shell
islocalizer install auto -m net6.0 -l zh-cn -cc LocaleFirst
```

### 3. build the local Localized IntelliSense files yourself

```shell
islocalizer build
```
This command may take a whole day... But when cached all page it will be completed faster.
The archive package will be saved in the default output directory. You can found the path in console.

### 4. install builded file
```shell
islocalizer install {ArchivePackagePath}
```
`ArchivePackagePath` is the path of the archive package that you builded.

#### run `islocalizer -h` to see more command and helps.