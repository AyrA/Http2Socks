# H2S: HTTP to SOCKS reverse proxy backend for onion services

This application provides a backend to make onion services accessible via a regular web browser.

This file is only an overview. For detailed instructions, read the files in the "docs" folder.

## Limitations

This application has a few limitations as shown below

### HTTP proxy

This application is not a full HTTP proxy.
It implements HTTP only to the point where requests can be forwarded into the Tor network.
This means it will not respect the `CONNECT` method for example.
The implementation of this method is not difficult, but it differs from the others.

There's no support for `Connection: keep-alive` either.
Configure your web server to not use keep-alive for the reverse proxy connections.

Or if you feel adventurous, implement a full HTTP parser and create a pull request for it.

### Access limitation

There are no IP based access control mechanisms in this application.
It's intended to run from a loopback interface only.

A blacklist functionality is provided for onion domains.

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

*Note: The seemingly unnecessary space after every "=" is in fact necessary*

Optionally, set a description

```bat
sc description Http2Socks "AyrA/Http2Socks: HTTP reverse proxy to forward .onion requests to Tor"
```

Edit `config.ini` now.
After this, you can revoke write access from the directory if you want to but in that case make sure the blacklist is stored in a location that stays writable if you plan on updating the list at runtime using the control connection.
The service runs under a limited user account, so revoking write access for everyone except "SYSTEM" and "Administrators" usually does the trick.

After you made the appropriate configuration changes, you can start the service again.

## Configuration

The service is configured using a `config.ini` file. For details see "docs/Config.md"

## Reverse Proxy

This tool is meant to work with a reverse proxy (often Apache or Nginx).

"docs/Apache.md" contains an example configuration for Apache.

## DNS

Configure your domain so `*.onion.yourdomain` points to your HTTP server.
The simplest approach is to create a CNAME record for `*.onion` that points to `yourdomain`.

## Blacklist

This application has blacklist functionality. The blacklist is configured in `config.ini`.
By default, users can access all onion HTTP services.

The blacklist is read by the service upon startup. The file is in INI format.

If a user tries to access a blacklsited domain, an appropriate HTTP error is returned.
The service will not attempt to look up the domain in the network at all.

### Entries

Each entry is in the following format:

```ini
[long.onion]
Name=Name of the onion service
Notes=Private information goes here
Reason=403
URL=https://example/url/to/reason/for/blacklisting
```

#### Section

The section name is the onion domain. The `.onion` part is optional.

Note: When saving the list using `BLSAVE` it will add `.onion` if missing and convert the domain to lowercase.

#### Setting `Name`

A name or description of the service.
This is displayed to the user on error pages.
This setting is optional.

#### Setting `Notes`

This is a field where you can store personal notes.
They're never shown to the user.
This setting is optional.

#### Setting `Reason`

This is the HTTP error reason code. It can be either 403 or 451.
- 403: Forbidden; Generic "go away" code
- 451: Unavailable for legal reasons; Used if you block this resource for legal reasons.

Rule of thumb is that 403 is because you don't want to serve this content and 451 is because someone else doesn't want you to serve this content.

#### Setting `URL`

This is the URL to the document/page stating the reason why this domain is blacklisted.
This setting is optional.

Recommended documents for given reasons:

- 403: Link to a document explaining why you decided to block this onion, or link to the relevant section of your TOS
- 451: Link to the document from the entity that issued the request

## Alias

This application has alias functionality. The alis list is configured in `config.ini`.
By default, no onion domains are aliased.

The alias list is read by the service upon startup. The file is in INI format.

If a user tries to access an alias of a domain, the user is either redirected to the full domain,
or the domain is transparently rewritten at the backend, depending on the "Type" value (see below).

### Entries

Each entry is in the following format:

```ini
[alias]
Onion=long.onion
Type=0
```

#### Section

The section name is the alias without the `.onion` TLD.

#### Setting `Onion`

This is the main `.onion` domain.

Note: When saving the list using `ALSAVE` it will add `.onion` if missing and convert the domain to lowercase.

#### Setting `Type`

This declares how to handle this alias.
It can be one of these values:

- 0 or "Rewrite": Transparently rewrites the domain. The short domain remains in the browser address bar
- 1 or "Redirect": Redirects the user to the full .onion. The real onion name will be put in the browser address bar

## Testing

I run a small example site for people to test:

`ewjipqsovlf7nuy3qeomovtnoqop2f2hkugtjxvtau5su4cjdgvjygid.onion`

It will display a message message similar to `example.com`
