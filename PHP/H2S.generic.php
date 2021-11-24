<?php
	//Generic Control connection commands


	//Performs authentication using the supplied password or cookie file content
	function H2S_auth($fp,$password){
		list($ok,$result)=H2S_sendCommand($fp,"AUTH $password");
		return $ok;
	}

	//Gets the protocol version
	function H2S_version($fp){
		list($ok,$data)=H2S_sendCommand($fp,'VERSION');
		if($ok){
			return +$data[0];
		}
		return FALSE;
	}

	//Performs no operation
	function H2S_noop($fp){
		list($ok,$data)=H2S_sendCommand($fp,'NOOP');
		return $ok;
	}
