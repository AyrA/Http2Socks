<?php

	//Control port library
	//====================
	//See docs/Library.md for details


	//Define CRLF constant. Whether you like it or not,
	//most text based protocols (HTTP, SMTP, etc) use CRLF.
	//Http2Socks follows this trend too.
	if(!defined('CRLF')){
		define('CRLF',"\r\n");
	}
	//Setting this to H2S_DEBUG to TRUE before including this file makes it print all protocol data.
	//Data is printed as "DBG: --> request" and "DBG: <-- response" to the console.
	//Note: This includes authentication data
	if(!defined('H2S_DEBUG')){
		define('H2S_DEBUG',FALSE);
	}

	//Commands have been split into categories to stop this file from growing too much
	foreach(glob(__DIR__ . '/H2S.*.php') as $inc){
		require($inc);
	}

	//Sends a raw command over the connection
	//returns an array with two values. First is a boolean to indicate success, second the raw response.
	//Use as: list($ok,$data)=H2S_sendCommand(...)
	function H2S_sendCommand($socket,$command){
		//By making the command optional we can use this function to also read the greeting.
		//You never want to do this yourself though as the greeting is the only time where NULL is appropriate.
		if($command!==NULL){
			if(H2S_DEBUG){
				echo "DBG: --> $command" . CRLF;
			}
			fwrite($socket,$command . CRLF);
		}
		$reply=array();
		do{
			$line=rtrim(fgets($socket,1024),CRLF);
			$reply[]=$line;
			if(H2S_DEBUG){
				echo "DBG: <-- $line" . CRLF;
			}
		}while($line!=='OK' && $line!=='ERR' && !feof($socket) && $line!==NULL);
		if($line!==NULL){
			array_pop($reply);
		}
		return array($line==='OK',$reply);
	}

	//Connects to the given IP and port.
	//The arguments are optional and are the default values for Http2Socks
	function H2S_connect($ip='127.0.0.1',$port=12244,$errno=NULL,$errstr=NULL){
		if($fp=@fsockopen("tcp://$ip",$port,$errno,$errstr,5)){
			list($ok,$greeting)=H2S_sendCommand($fp,NULL);
			if($ok){
				return $fp;
			}
			fclose($fp);
		}
		return FALSE;
	}

	//Closes the connection
	function H2S_close($fp){
		list($ok)=H2S_sendCommand($fp,'EXIT');
		fclose($fp);
		return $ok;
	}

	//URL encodes an argument. Useful if an argument contains spaces.
	function H2S_encode($str){
		if($str===NULL){
			return NULL;
		}
		return str_replace('%20','+',urlencode($str));
	}
