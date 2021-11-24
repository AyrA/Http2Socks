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
			return +explode(' ',$data[0])[0];
		}
		return FALSE;
	}

	//Performs no operation
	function H2S_noop($fp){
		list($ok,$data)=H2S_sendCommand($fp,'NOOP');
		return $ok;
	}

	//Halts HTTP processing
	function H2S_halt($fp){
		list($ok,$data)=H2S_sendCommand($fp,'HALT');
		return $ok;
	}

	//Continues HTTP processing
	function H2S_cont($fp){
		list($ok,$data)=H2S_sendCommand($fp,'CONT');
		return $ok;
	}

	//Get process info
	function H2S_info($fp){
		list($ok,$data)=H2S_sendCommand($fp,'INFO');
		return $ok?parse_ini_string(implode(CRLF,$data),FALSE,INI_SCANNER_RAW):FALSE;
	}
