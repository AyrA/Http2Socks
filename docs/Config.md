# Configuration

The configuration is done in `config.ini`. The file is created if it doesn't exists.
It's only read when the service is started, so be sure to restart the service after you make changes while the service is already running.
Except for creating the default file, the service never writes to this file.

**The section and setting names are case and whitespace sensitive**

## Section `TOR`

This section holds configuration for connecting to your local Tor client.

### Setting `IP`

This is the IP of your local Tor SOCKS instance, usually `127.0.0.1`

### Setting `Port`

This is the port of the local Tor SOCKS server, usually `9050`

### Setting `Timeout`

Number of milliseconds to wait for connections to Tor.
The default is `5000`.
This setting has mostly no effect if you run Tor on the local machine and connect via loopback.

If Tor runs on another computer, you can use this value to not have the user wait for 20+ seconds if the Tor client happens to be unreachable.

Note: This setting only affects the connection establishment.
There is no Timeout for onion services that are slow or not responding.

## Section `HTTP`

Settings for the local HTTP listener

### Setting `IP`

This is the IP where to listen for connections, usually `127.0.0.1`

Do not set to an IP that's reachable from the outside unless you know exactly what you're doing.

### Setting `Port`

This is the port of the local HTTP listener.
Default is `12243`

## Section `DNS`

This is the DNS specific configuration.

### Setting `Suffix`

This is the DNS suffix used to make your service reachable.

In other words, the part after `.onion` that users need to append to the domain to land on your server.

Example: If people can reach `abc...def.onion` by going to `abc...def.onion.example.com`,
set this to `example.com` (without the leading dot)

H2S will reject any attempt to try to reach a different domain.

### Setting `Blacklist`

A file path to a blacklist file.

The file contains a list of onion domains that are blacklisted. Users cannot access domains from that list.
This functionality is ignored if the setting is missing, empty, or the file doesn't exist.
The format of the file is described in the Blacklist chapter further below.

## Section `Control`

This section holds configuration for the control port.
The entire section is optional. If it's missing, no control port is offered.
The control protocol is explained further below, but it's a simple line based protocol.

### Setting `IP`

This is the IP where to listen for connections, usually `127.0.0.1`

### Setting `Port`

This is the port for the control connection listener. Default is `12244`

### Setting `Password`

This is the password used for authentication.

You can type the password in the clear. The application will then hash it and replace the setting.
The entry format for a hashed password is: ENC:salt:hash

- ENC is literal "ENC"
- salt is a salt as Base64 encoded
- hash is the result of HMAC(salt,password) as Base64 encoded

You can leave this absent to disable password based authentication.
If this entry is absent, the "Cookie" setting must be set.

### Setting `Cookie`

This is the path to a file that holds the cookie string.
The file is generated every time the service starts and holds a single string.
The string in the file is guaranteed to be ASCII compatible.

The file is never read by the service.
You can at any point delete it if you want to stop other applications from accessing the service.
The only way to re-create the file is to restart the service.
