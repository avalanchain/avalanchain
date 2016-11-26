(function() {
    'use strict';
    var serviceId = 'websocketservice';
    angular.module('avalanchain').factory(serviceId, ['$http', '$q', 'common', 'dataProvider', '$filter', '$timeout', '$websocket', '$window', websocketservice]);

    function websocketservice($http, $q, common, dataProvider, $filter, $timeout, $websocket, $window) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn('websocketservice');

        function Listeners(path, listeners) {
            var host = $window.location.host;
            this.path = path ? 'ws://' + host + path: 'ws://' + host + '/ws/yahoo';
            this.listeners = listeners || [];
        }
        Listeners.prototype = {
            addListener: function (l) { this.listeners.push(l); },
            removeListener: function(l) {
                this.listeners.pop(l);
            }
        };

        var ylisteners = new Listeners();
        // var listeners = function () {};
        // listeners.addListener = function(l) {
        //     listeners.push(l);
        // }
        //return service;
        var host = $window.location.host;
        var dataStream = $websocket('ws://' + host + '/ws/yahoo');

        var collection = [];

        dataStream.onMessage(function(message) {
            angular.forEach(ylisteners.listeners, function(l) {
                $timeout(function () {
                    l.newData(JSON.parse(message.data));
                });
                // if (collection.length  > 100) {
                //     collection.pop();
                // }
                // collection.unshift(JSON.parse(message.data));
                // collection.push(JSON.parse(message.data));
            })
        });

        var methods = {
            collection: collection,
            ylisteners: ylisteners,
            get: function() {
                dataStream.send(JSON.stringify({ action: 'get' }));
            }
        };
        return methods;




    }
})();
