(function() {
    'use strict';
    var serviceId = 'websocketservice';
    angular.module('avalanchain').factory(serviceId, ['$http', '$q', 'common', 'dataProvider', '$filter', '$timeout', '$websocket', '$window', websocketservice]);

    function websocketservice($http, $q, common, dataProvider, $filter, $timeout, $websocket, $window) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn('websocketservice');

        function Listeners(path, listeners, stream) {
            var host = $window.location.host;
            this.path = path ? 'ws://' + host + path: 'ws://' + host + '/ws/yahoo';
            this.stream = $websocket(this.path);
            this.listeners = listeners || [];

            // this.stream.onMessage(function(message) {
            //     angular.forEach(listeners, function(l) {
            //         $timeout(function () {
            //             l.newData(JSON.parse(message.data));
            //         });
            //     })
            // })
        }
        Listeners.prototype = {
            addListener: function (l) { this.listeners.push(l); },
            removeListener: function(l) {
                this.listeners.pop(l);
            },
            onMessage: function (l){

            }
        };

        var ylisteners = new Listeners('/ws/yahoo');
        var nlisteners = new Listeners('/ws/nodes');

        ylisteners.stream.onMessage(function(message) {
            angular.forEach(ylisteners.listeners, function(l) {
                $timeout(function () {
                    l.newData(JSON.parse(message.data));
                });
            })
        });

        nlisteners.stream.onMessage(function(message) {
            angular.forEach(nlisteners.listeners, function(l) {
                $timeout(function () {
                    l.newData(JSON.parse(message.data));
                });
            })
        });

        var methods = {
            nlisteners: nlisteners,
            ylisteners: ylisteners,
            get: function() {
                ylisteners.stream.send(JSON.stringify({ action: 'get' }));
            }
        };
        return methods;




    }
})();
