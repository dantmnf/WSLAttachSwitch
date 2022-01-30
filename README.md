WSLAttachSwitch
===============

## UPDATE
This feature is officially available on WSL2 preview, see https://github.com/microsoft/WSL/issues/4150#issuecomment-1018524753 for details.

## Overview
This tool attaches the WSL2 virtual machine to a Hyper-V virtual switch.

Get prebuilt binaries from [Releases](https://github.com/dantmnf/WSLAttachSwitch/releases) or Actions artifacts [![build](https://github.com/dantmnf/WSLAttachSwitch/actions/workflows/build.yml/badge.svg)](https://github.com/dantmnf/WSLAttachSwitch/actions/workflows/build.yml).

## Example
```console
root@WSL ~ # # Check existing interface
root@WSL ~ # ip link 
1: lo: <LOOPBACK,UP,LOWER_UP> mtu 65536 qdisc noqueue state UNKNOWN mode DEFAULT group default qlen 1000

......

6: eth0: <BROADCAST,MULTICAST,UP,LOWER_UP> mtu 1500 qdisc mq state UP mode DEFAULT group default qlen 1000
    link/ether 00:15:5d:f3:58:46 brd ff:ff:ff:ff:ff:ff
root@WSL ~ # # Attach to Hyper-V virtual switch "New Virtual Switch"
root@WSL ~ # /mnt/c/some/random/path/WSLAttachSwitch.exe "New Virtual Switch"
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

## Notes

This tool needs to be run again if the WSL VM has been restarted.
