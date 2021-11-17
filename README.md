# H2S: HTTP to SOCKS reverse proxy backend for onion services

This application provides a backend to make onion services accessible via a regular web browser.

## Limitations

This application has a few limitations as shown below

### HTTP proxy

This application is not a full HTTP proxy.
It implements HTTP only to the point where requests can be forwarded into the Tor network.
This means it will not respect the `CONNECT` method for example.
The implementation of this method is not difficult, but it differs from the others.

### Access limitation

There are no IP based access control mechanisms in this application.
It's intended to run from a loopback interface only.

### Tor

This application doesn't contains Tor.
If you need help installing Tor as a Windows service, [check this article](https://cable.ayra.ch/help/fs.php?help=tor).
It also contains a default file that configures Tor as a local SOCKS proxy.

## Installation

[Download the executable](https://gitload.net/AyrA/Http2Socks) and place it somewhere convenient,
preferrably a path without spaces in it because it makes the service command easier.
Make sure the directory you put it in is not write protected,
or it fails to create the default configuration.

Open an administrative command prompt in the directory you placed the executable in
and run these commands:

```bat
sc create Http2Socks binPath= "%CD%\H2S.exe" start= auto depend= tcpip obj= "NT AUTHORITY\LocalService"
net start Http2Socks
net stop Http2Socks
```

Optionally, set a description

```bat
sc description Http2Socks "AyrA/Http2Socks: HTTP Reverse proxy to forward .onion requests to Tor"
```

Edit `config.ini` now.
After this, you can revoke write access from the directory if you want to.
The service runs under a limited user account, so revoking write access for regular users is fine.

After you made the appropriate configuration changes, you can start the service again.

## Configuration

The configuration is done in `config.ini`. The file is created if it doesn't exists.
It's only read when the service is started, so be sure to restart the service after you make changes while the service is already running.
Except for creating the default file, the service never writes to this file.

**The section and setting names are case and whitespace sensitive**

### Section `TOR`

This section holds configuration for connecting to your local Tor client.

#### Setting `IP`

This is the IP of your local Tor SOCKS instance, usually `127.0.0.1`

#### Setting `Port`

This is the port of the local Tor SOCKS server, usually `9050`

#### Setting `Timeout`

Number of milliseconds to wait for connections to Tor.
The default is `5000`.
This setting has mostly no effect if you run Tor on the local machine and connect via loopback.

If Tor runs on another computer, you can use this value to not have the user wait for 20+ seconds if the Tor client happens to be unreachable.

Note: This setting only affects the connection establishment.
There is no Timeout for onion services that are slow or not responding.

### Section `HTTP`

Settings for the local HTTP listener

#### Setting `IP`

This is the IP where to listen for connections, usually `127.0.0.1`

Do not set to an IP that's reachable from the outside unless you know exactly what you're doing.

#### Setting `Port`

This is the port of the local HTTP listener.
Default is `12243`

### Section `DNS`

This is the DNS specific configuration.
It has only one setting as of now.

#### Setting `Suffix`

This is the DNS suffix used to make your service reachable.

In other words, the part after `.onion` that users need to append to the domain to land on your server.

Example: If people can reach `abc...def.onion` by going to `abc...def.onion.example.com`,
set this to `example.com` (without the leading dot)

H2S will reject any attempt to try to reach a different domain.

## Setting up the reverse proxy

How exactly this is done depends on your HTTP server.
Here is the configuration you need to add if you run Apache.
You may need to enable some modules for this to work.

Replace the name and alias with the suffix you configured.
If your suffix is `example.com`, The ServerName should read `onion.example.com`,
and the ServerAlias should be `*.onion.example.com`

Replace the IP and Port in the `ProxyPass` line with the IP and port if you changed the default configuration.

```apache
#.onion forwarder Demo
<VirtualHost *:443>
	ServerName onion.example.com
	ServerAlias *.onion.example.com
	UseCanonicalName Off
	ProxyPass "/" "http://127.0.0.1:12243/"
	ProxyPreserveHost On
	ProxyTimeout 60
	# Can use any directory here. It's never accessed
	DocumentRoot "${SRVROOT}/htdocs"
</VirtualHost>
```

Note: The configuration above lacks SSL specific settings to make it shorter.

## DNS

Configure your domain so `*.onion.yourdomain` points to your HTTP server.

## Testing

The Tor project runs its website on an onion service. You can use that to test your setup.
Just ad your suffix to the domain below:

http://2gzyxa5ihm7nsggfxnu52rck2vv4rvmdlkiu3zzui5du4xyclen53wid.onion
