(function() {
    'use strict';
    var serviceId = 'websocketservice';
    angular.module('avalanchain').factory(serviceId, ['$http', '$q', 'common', 'dataProvider', '$filter', '$timeout', '$websocket', '$window', websocketservice]);

    function websocketservice($http, $q, common, dataProvider, $filter, $timeout, $websocket, $window) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn('websocketservice');

        function Listeners(path, name, listeners, stream) {
            var host = $window.location.host;
            this.name = name|| 'websocket';
            this.path = path ? 'ws://' + host + path: 'ws://' + host + '/ws/yahoo';
            this.stream = $websocket(this.path);
            this.listeners = listeners || [];
            this.stream.onOpen(function() {
                console.log('connection open: ' + this.url);
            });
            this.stream.onClose(function(event) {
                console.log('connection closed', event);
            });
            this.stream.onError(function(event) {
                console.log('connection Error', event);
            });
        }
        Listeners.prototype = {
            addListener: function (l) { this.listeners.push(l); },
            removeListener: function(l) {
                this.listeners.pop(l);
            },
            onMessage: function (l){

            }
        };

        var ylisteners = new Listeners('/ws/yahoo', 'yahoo');
        var nlisteners = new Listeners('/ws/nodes', 'nodes');
        var mlisteners = new Listeners('/ws/chat', 'chat');


        ylisteners.stream.onMessage(function(message) {
            angular.forEach(ylisteners.listeners, function(l) {
                $timeout(function () {
                    l.newData(JSON.parse(message.data));
                });
            })
        });

        mlisteners.stream.onMessage(function(message) {
            angular.forEach(mlisteners.listeners, function(l) {
                $timeout(function () {
                    l.newData(JSON.parse(message.data));
                });
            })
        });

        nlisteners.stream.onMessage(function(message) {
            var mes = JSON.parse(message.data);
            if(!mes.NodeUp){
                log('NODE ADDED Port: ' + mes.NodeJoined.address.port);
            }
            angular.forEach(nlisteners.listeners, function(l) {
                $timeout(function () {
                    l.newData(mes);
                });
            })
        });

        var methods = {
            nlisteners: nlisteners,
            ylisteners: ylisteners,
            mlisteners: mlisteners,
            get: function() {
                ylisteners.stream.send(JSON.stringify({ action: 'get' }));
            }
        };
        return methods;




    }
})();
