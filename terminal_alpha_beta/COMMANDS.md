```
[target.armv7-unknown-linux-gnueabihf]
linker = "arm-linux-gnueabihf-gcc"
```
```
[target.armv7-unknown-linux-musleabihf]
linker = "arm-linux-gnueabihf-gcc"
```
```
snips-nlu train path/to/data.json path/to/output_folder
```

Manage Systemctl Services
```
systemctl list-unit-files --type service -all
sudo systemctl start service.service
sudo systemctl status service.service
sudo systemctl stop service.service
```

```
scp -P 11945 target/aarch64-unknown-linux-musl/release/terminal_alpha_beta zireael9797@192.168.88.94:terminal_alpha_beta
```
```
scp -P 11945 data/responses.json zireael9797@192.168.88.94:responses.json
```
```
scp -P 5914 -r data/rootengine  alarm@192.168.0.104:rootengine
```

Systemd Service
```
[Unit]
Description=Terminal Alpha Beta Service
After=network.target

[Service]
User=root
WorkingDirectory=/home/zireael9797/bot
ExecStart=/home/zireael9797/bot/terminal_alpha_beta
Restart=always

[Install]
WantedBy=multi-user.target
```
```
dependencies on arch/manjaro for
------
arm-musl-release:
	 CARGO_TARGET_ARMV7_UNKNOWN_LINUX_MUSLEABIHF_LINKER=arm-linux-gnueabihf-gcc CC_armv7_unknown_linux_musleabihf=arm-linux-gnueabihf-gcc cargo build --target armv7-unknown-linux-musleabihf --release
```
```
Needed
------
> arm-linux-gnueabihf-gcc
>		arm-linux-gnueabihf-binutils
>		arm-linux-gnueabihf-gcc-stage1
>		arm-linux-gnueabihf-linux-api-headers
>		arm-linux-gnueabihf-glibc-headers
>		arm-linux-gnueabihf-gcc-stage2
>		arm-linux-gnueabihf-glibc
>		arm-linux-gnueabihf-gcc
> clang
> openssl (? was already installed, shouldn't risk uninstall testing, probably important to OS itself)
```
```
Needed for training intent
------
> Python 3.7+
> `pip install snips-nlu`
> `python -m snips_nlu download en` or simply `snips-nlu download en`
```
```
Not Needed (Probably, further testing required)
------
> arm-linux-gnueabihf-musl
> crfsuite (unsure - test by removing)
```