(function() {
    'use strict';
    var serviceId = 'websocetservice';
    angular.module('avalanchain').factory(serviceId, ['$http', '$q', 'common', 'dataProvider', '$filter', '$timeout', '$websocket', websocetservice]);

    function websocetservice($http, $q, common, dataProvider, $filter, $timeout, $websocket) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn('dataservice');
        var key = '616161';
        var service = {
            getYahoo: getYahoo,
        };

        //return service;

        var dataStream = $websocket('ws://localhost:9911/ws/yahoo');

        var collection = [];

        dataStream.onMessage(function(message) {
            collection.push(JSON.parse(message.data));
        });

        var methods = {
            collection: collection,
            get: function() {
                dataStream.send(JSON.stringify({ action: 'get' }));
            }
        };
        return methods;
        function getYahoo() {
            return collection;
        }


        function publicKey() {

            return {
                get: function() {
                    return key;
                },
                set: function(value) {
                    key = value;
                }
            }
        }



    }
})();
