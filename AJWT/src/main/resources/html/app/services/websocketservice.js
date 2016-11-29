(function() {
    'use strict';
    var serviceId = 'websocketservice';
    angular.module('avalanchain').factory(serviceId, ['$http', '$q', 'common', 'dataProvider', '$filter', '$timeout', '$websocket', '$window', websocketservice]);

    function websocketservice($http, $q, common, dataProvider, $filter, $timeout, $websocket, $window) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn('websocketservice');

        function Listeners(path, name, listeners, stream) {
            var host = $window.location.host;
            this.path = path ? 'ws://' + host + path : 'ws://' + host + '/ws/yahoo';
            this.stream = $websocket(this.path);
            this.listeners = listeners || [];
            this.stream.onOpen(function() {
                console.log('connection open: ' + this.url);
            });
            this.stream.onClose(function(event) {
                console.log('connection closed', event);
                reopenConn(this.url);
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

        var ylisteners = new Listeners('/ws/yahoo');
        var nlisteners = new Listeners('/ws/nodes');
        var mlisteners = new Listeners('/ws/chat');
        // var alisteners = new Listeners('/ws/accounts');
        var tlisteners = new Listeners('/ws/transactions');


        function reopenConn(url) {
            var host = $window.location.host;
            var path  = url.replace('ws://' + host ,'');
            switch(path) {
                case '/ws/yahoo':
                    // ylisteners.close();
                    ylisteners = new Listeners('/ws/yahoo');
                    break;
                case '/ws/nodes':
                    // nlisteners.close();
                    nlisteners = new Listeners('/ws/nodes');
                    break;
                case '/ws/chat':
                    // mlisteners.close();
                    mlisteners = new Listeners('/ws/chat');
                    break;
                // case '/ws/accounts':
                //     // mlisteners.close();
                //     alisteners = new Listeners('/ws/accounts');
                //     break;
                case '/ws/accounts':
                    // mlisteners.close();
                    tlisteners = new Listeners('/ws/transactions');
                    break;
            }

        }

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
                    l.newMesData(JSON.parse(message.data));
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
                    l.nodeData(mes);
                });
            })
        });

        // alisteners.stream.onMessage(function(message) {
        //     angular.forEach(alisteners.listeners, function(l) {
        //         $timeout(function () {
        //             l.accountData(JSON.parse(message.data));
        //         });
        //     })
        // });

        tlisteners.stream.onMessage(function(message) {
            angular.forEach(tlisteners.listeners, function(l) {
                $timeout(function () {
                    l.transactionData(JSON.parse(message.data));
                });
            })
        });

        var methods = {
            nlisteners: nlisteners,
            ylisteners: ylisteners,
            mlisteners: mlisteners,
            // alisteners: alisteners,
            tlisteners: tlisteners,
            get: function() {
                ylisteners.stream.send(JSON.stringify({ action: 'get' }));
            }
        };
        return methods;




    }
})();
