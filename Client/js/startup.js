/*
Copyright 2011 Olivine Labs, LLC. <http://olivinelabs.com>
Licensed under the MIT license: <http://www.opensource.org/licenses/mit-license.php>
*/

(function(window, $) {
  // Set URL of your WebSocketMain.swf here, for web-socket-js
  WEB_SOCKET_SWF_LOCATION = 'js/WebSocketMainInsecure.swf';
  var AlchemyChatServer = {};
  var me = {};

  function Connect() {

    // If we're using the Flash fallback, we need Flash.
    if (!window.WebSocket && !swfobject.hasFlashPlayerVersion('10.0.0')) {
      alert('Flash Player >= 10.0.0 is required.');
      return;
    }

    // Set up the Alchemy client object
    AlchemyChatServer = new Alchemy({
      Server: $('#server').val(),
      Port: $('#port').val(),
      Action: 'chat',
      DebugMode: true
    });

    LogMessage('Connecting...');
    $('#status').removeClass('offline').addClass('pending').text('Connecting...');

    AlchemyChatServer.Connected = function() {
      LogMessage('Connection established!');
      $('#status').removeClass('pending').addClass('online').text('Online');
      $('#connectToServer').hide('fast', function() { $('#registerName').show('fast'); });
    };

    AlchemyChatServer.Disconnected = function() {
      LogMessage('Connection closed.');
      $('#status').removeClass('pending').removeClass('online').addClass('offline').text('Offline');
      $('#onlineUsers').text('???');
      $('#registerName, #sendMessage').hide('fast', function() { $('#connectToServer').show('fast'); });
    };

    AlchemyChatServer.MessageReceived = function(event) {
      ParseResponse(event.data);
    };

    AlchemyChatServer.Start();
  };

  function LogMessage(message) {
    var p = $('<p></p>').text(message);
    $('#results').prepend(p);
  }

  function ParseResponse(response) {
    var data = JSON.parse(response);

    // The Chat server demo sends back a responsetype to let us know how to parse the message.
    if (data.Type == 0) {
      var message = data.Data.Name + ' connected!';
      LogMessage(message);
    }else if (data.Type == 1) {
      var message = data.Data.Name + ' disconnected!';
      LogMessage(message);
    }else if (data.Type == 2 && data.Data.Name != me.Name) {
      // We don't display it if it's from ourselves, because we display our own messages immediately
      // see the jQuery bindings later on for more info)
      var message = data.Data.Name + ': ' + data.Data.Message;
      LogMessage(message);
    }else if (data.Type == 3) {
      var message = data.Data.Message;
      LogMessage(message);
    }else if (data.Type == 4) {
      // Set the online users, and show the list of users if you hover over the number.
      $('#onlineUsers').text(data.Data.Users.length).attr('title', data.Data.Users.join('\n'));
    }
  }

  function ValidateName(name) {
    if (name.length < 3 || name.length > 25) {
      return false;
    }

    return true;
  }

  // Just some event bindings.
  $(function() {
    $('#sendMessage').bind('submit', function(e) {
      e.preventDefault();

      var message = $('#message').val();
      var data = {};

      if (message.indexOf('/nick ') == 0) {
        var name = message.replace(/\/nick /, '');
        data = { Type: 2, Name: name };

        if (!ValidateName(name)) {
          alert('Please pick a name of length 3 - 25.');
          return;
        }

        if (name == me.Name) {
          return;
        }

        me.Name = name;
      }else {
        // We display our own messages immediately to increase perceived performance.
        data = { Type: 1, Message: message };
        LogMessage(me.Name + ': ' + data.Message);
      }

      AlchemyChatServer.Send(data);

      $('#message').val('').focus();
    });

    $('#registerName').bind('submit', function(e) {
      e.preventDefault();
      var name = $('#name').val();

      if (!ValidateName(name)) {
        alert('Please pick a name of length 3 - 25.');
        return;
      }

      me.Name = name;
      var data = { Type: 0, Name: name };

      AlchemyChatServer.Send(data);

      $('#registerName').hide('fast', function() { $('#sendMessage').show('fast'); });
    });

    $('#connectToServer').bind('submit', function(e) {
      e.preventDefault();
      Connect();
    });

    $('#disconnect').bind('click', function(e) {
      e.preventDefault();
      AlchemyChatServer.Stop();
    });
  });
})(window, jQuery);
