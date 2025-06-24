WSLAttachSwitch
===============

## Overview

This tool is expected to be used to **attach any number of Hyper-V virtual switches** to a WSL2 virtual machine (`eth1, eth2, eth3, ...`).

Get prebuilt binaries from [Releases](https://github.com/dantmnf/WSLAttachSwitch/releases) or Actions artifacts [![build](https://github.com/dantmnf/WSLAttachSwitch/actions/workflows/build.yml/badge.svg)](https://github.com/dantmnf/WSLAttachSwitch/actions/workflows/build.yml).

If you are unfamiliar with Hyper-V networking, check the [Microsoft guide](https://learn.microsoft.com/en-us/windows-server/virtualization/hyper-v/get-started/create-a-virtual-switch-for-hyper-v-virtual-machines?tabs=hyper-v-manager) for some fundamental concepts.

If your need is not to attach multiple switches, but to replace the switch to which the default (`eth0`) NIC is attached,
then it is officially available on WSL2 preview, check [this comment](https://github.com/microsoft/WSL/issues/4150#issuecomment-1018524753) for details.

## Arguments
```
--mac <mac>     If specified, use this physical address for the virtual interface instead of random one.
--vlan <vlan>   If specified, enable VLAN filtering with this VLAN ID for the virtual interface.
--save-params   If specified, will save the passed parameters to %appdata%\WSLAttachSwitch\params.json (will be read from there if no network name is passed when the tool is launched afterwards), alternatively -s as short form
```

## Example
```console
root@WSL ~ # # Check existing interface
root@WSL ~ # ip link 
1: lo: <LOOPBACK,UP,LOWER_UP> mtu 65536 qdisc noqueue state UNKNOWN mode DEFAULT group default qlen 1000

......

6: eth0: <BROADCAST,MULTICAST,UP,LOWER_UP> mtu 1500 qdisc mq state UP mode DEFAULT group default qlen 1000
    link/ether 00:15:5d:f3:58:46 brd ff:ff:ff:ff:ff:ff
root@WSL ~ # # Assume that we have a virtual switch named "New Virtual Switch" in Hyper-V Manager
root@WSL ~ # cmd.exe /c "c:\some\random\path\WSLAttachSwitch.exe" "New Virtual Switch"
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

All the following invocations are supported:
```
WSLAttachSwitch.exe "New Virtual Switch"
WSLAttachSwitch.exe "New Virtual Switch" --mac 00-11-45-14-19-19
WSLAttachSwitch.exe --mac 00:11:45:14:19:19 "New Virtual Switch"
WSLAttachSwitch.exe --mac 0011.4514.1919 "New Virtual Switch" --vlan 2
WSLAttachSwitch.exe "New Virtual Switch" --save-params
WSLAttachSwitch.exe #If the tool was previously invoked with --save-params, then it will re-use the parameters from that invocation
```

## Notes

This tool needs to be run again if the WSL VM has been restarted.
