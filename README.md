WSLAttachSwitch
===============

## Overview

This tool is expected to be used to **attach any number of Hyper-V virtual switches** to a WSL2 virtual machine (`eth1, eth2, eth3, ...`).

Get prebuilt binaries from [Releases](https://github.com/dantmnf/WSLAttachSwitch/releases) or Actions artifacts [![build](https://github.com/dantmnf/WSLAttachSwitch/actions/workflows/build.yml/badge.svg)](https://github.com/dantmnf/WSLAttachSwitch/actions/workflows/build.yml).

If your need is not to attach multiple switches, but to replace the switch to which the default (`eth0`) NIC is attached,
then it is officially available on WSL2 preview, check [this comment](https://github.com/microsoft/WSL/issues/4150#issuecomment-1018524753) for details.

## Arguments
```
--network <network>          Network name or GUID. Example: Ethernet
--mac-address <mac-address>  Optional. Fix physical address of network interface to this mac address if specificated. Example: 00-11-45-14-19-19
--version                    Show version information
-?, -h, --help               Show help and usage information
```

## Example
```console
root@WSL ~ # # Check existing interface
root@WSL ~ # ip link 
1: lo: <LOOPBACK,UP,LOWER_UP> mtu 65536 qdisc noqueue state UNKNOWN mode DEFAULT group default qlen 1000

......

6: eth0: <BROADCAST,MULTICAST,UP,LOWER_UP> mtu 1500 qdisc mq state UP mode DEFAULT group default qlen 1000
    link/ether 00:15:5d:f3:58:46 brd ff:ff:ff:ff:ff:ff
root@WSL ~ # # Attach to Hyper-V virtual switch "New Virtual Switch"
root@WSL ~ # /mnt/c/some/random/path/WSLAttachSwitch.exe --network "New Virtual Switch"
root@WSL ~ # # Now we have a new interface "eth1"
root@WSL ~ # ip link
1: lo: <LOOPBACK,UP,LOWER_UP> mtu 65536 qdisc noqueue state UNKNOWN mode DEFAULT group default qlen 1000

......

6: eth0: <BROADCAST,MULTICAST,UP,LOWER_UP> mtu 1500 qdisc mq state UP mode DEFAULT group default qlen 1000
    link/ether 00:15:5d:f3:58:46 brd ff:ff:ff:ff:ff:ff
7: eth1: <BROADCAST,MULTICAST> mtu 1500 qdisc noop state DOWN mode DEFAULT group default qlen 1000
    link/ether 00:15:5d:e0:01:0c brd ff:ff:ff:ff:ff:ff
root@WSL ~ # # Make use of new interface
root@WSL ~ # dhclient eth1
```

```
WSLAttachSwitch.exe --network Ethernet
WSLAttachSwitch.exe --network Ethernet --mac-address 00-11-45-14-19-19
WSLAttachSwitch.exe --network Ethernet --mac-address 00-11-45-14-19-19 --vlan-isolation-id 2
```

## Notes

This tool needs to be run again if the WSL VM has been restarted.
