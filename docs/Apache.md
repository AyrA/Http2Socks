# Apache as Reverse Proxy

*This document uses SSL and TLS interchangeably*

Configuration of apache is fairly simple.
The configuration shown here is for a server with an SSL certificate.

Replace the name and alias with the suffix you configured in Http2Socks.
If your suffix is `example.com`, The ServerName should read `onion.example.com`,
and the ServerAlias should be `*.onion.example.com`

Replace the IP and Port in the `ProxyPass` line with the IP and port if you changed the default configuration.

```apache
#.onion forwarder Demo with SSL enforcement
<VirtualHost *:80>
	ServerName onion.example.com
	ServerAlias *.onion.example.com
	DocumentRoot "${SRVROOT}/htdocs/null"
	#Redirect all connections to the HTTPS version.
	RewriteEngine On
	RewriteCond %{REQUEST_URI} !^/\.well\-known/acme\-challenge/
	RewriteRule ^(.*)$ https://%{HTTP_HOST}$1 [R=301,L]
</VirtualHost>
<VirtualHost *:443>
	SSLEngine On
	SSLCertificateFile C:/Path/To/Wildcard.crt
	SSLCertificateKeyFile C:/Path/To/Wildcard.key

	ServerName onion.example.com
	ServerAlias *.onion.example.com
	UseCanonicalName Off
	SetEnv proxy-nokeepalive 1
	ProxyPass "/" "http://127.0.0.1:12243/"
	ProxyPreserveHost On
	ProxyTimeout 60
	# Can use any directory here. It's never accessed
	DocumentRoot "${SRVROOT}/htdocs/null"
	# Support Websockets
	<IfModule proxy_wstunnel_module>
		RewriteEngine on
		RewriteCond %{HTTP:Upgrade} websocket [NC]
		RewriteCond %{HTTP:Connection} upgrade [NC]
		RewriteRule ^/?(.*) "ws://127.0.0.1:12243/$1" [P,L]
	<!IfModule>
</VirtualHost>
```

Note: The configuration above lacks most SSL specific settings to make it shorter.
Consider serving content over an encrypted connection only.
You need a wildcard certificate for your suffix for this to work.
They're around 50 USD per year, or your soul if you want to use certbot with Let's Encrypt certificates.

## Secure TLS

Make sure your server and users are not vulnerable to attacks on the TLS protocol.
You can use the Mozilla SSL confifgurator to get appropriate settings for apache:

https://ssl-config.mozilla.org/#server=apache&version=2.4.51&config=intermediate&openssl=1.1.1k&guideline=5.6

## Modules

You may need to enable additional modules, including but not limited to:

- mod_proxy
- mod_proxy_http
- mod_http2
- mod_ssl
- mod_env
- mod_socache_shmcb
- mod_rewrite
- mod_headers
- mod_proxy_wstunnel