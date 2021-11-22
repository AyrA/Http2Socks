<?php

	//This file demonstrates how to use the control connection
	//Actions performed:
	//1. Connect
	//2. Authenticate
	//3. Add an onion domain to the blacklist
	//4. Dump blacklist to console
	//5. Remove onion domain from blacklist
	//6. Save Blacklist
	//7. Disconnect

	require('H2S.php');
	function p($x){echo $x . CRLF;}

	//Cookie file path from the debug version.
	//You very likely need to change this to match your real file path
	//if you're not debugging and running this script from the default project path.
	$cookiefile=__DIR__ . '/../H2S/bin/Debug/cookie.txt';
	$cli=PHP_SAPI==='cli';

	if(!$cli){
		header('Content-Type: text/plain; charset=utf-8');
	}

	if(is_file($cookiefile)){
		$auth=@file_get_contents($cookiefile);
	}
	else{
		//You can use a password if no cookie file is present
		//Using a cookie file is generally preferred
		//as it automatically resets on every service restart,
		//which is preferrable to a hardcoded password.
		//If you have a safe way of storing the value,
		//you can read the cookie file and then delete it.
		//The service doesn't needs it.
		//More information can be found in the documentation
		//of the control connection part in the config.ini file.
		if($cli){
			//On the terminal, we can simply ask for the password
			//Note: PHP lacks the ability to mask the input,
			//      meaning the password is visible
			p('Cookie file not found.');
			if(function_exists('readline')){
				$auth=readline('Enter password instead: ');
			}
			else{
				echo 'Enter password instead: ';
				$auth=stream_get_line(STDIN, 1024, PHP_EOL);
			}
		}
		else{
			//Remove this line once you set a password
			die('[' . __FILE__ . ':' . __LINE__ . ']: Require password. Change file.');
			$auth='YourPasswordGoesHere';
		}
	}
	if(!$auth){
		die('Unable to read ' . $cookiefile . ', and no password has been set');
	}

	p('Connecting to API');
	if($h2s=H2S_connect()){
		//Construct a theoretically valid onion domain
		$example=str_pad('blocked',56,'x') . '.onion';
		p('Connected');
		$version=H2S_version($h2s) or die(1);
		p("API version: $version");
		H2S_auth($h2s,$auth) or die('Authentication failure. Password or cookie incorrect.');
		H2S_addToBlacklist($h2s,$example,451,'Test','No reason given','https://example.com/') or die('BLADD failed');
		print_r(H2S_getBlacklist($h2s));
		H2S_deleteFromBlacklist($h2s,$example) or die('Delete entry from blacklist failed');
		H2S_saveBlacklist($h2s) or die('Unable to save the blacklist');
		H2S_noop($h2s) or die('NOOP failed');
		H2S_close($h2s) or die('EXIT failed');
		p('Test completed');
	}
	else{
		p('Connection failed. Make sure H2S is running with its default control port IP and port settings.');
		p('As an alternative, edit ' . __FILE__ . ' to match your control connection settings.');
	}
