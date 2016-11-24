(function() {
    'use strict';
    var serviceId = 'jwtservice';
    angular.module('avalanchain').factory(serviceId, ['$http', '$q', 'common', 'dataProvider', '$filter', '$timeout', jwtservice]);

    function jwtservice($http, $q, common, dataProvider, $filter, $timeout) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn('dataservice');
        var key = '616161';
        var service = {
            generateJWS: generateJWS,
            verifyJWS: verifyJWS,
            generateJWT: generateJWT,
            verifyJWT: verifyJWT,
            getJWT: getJWT,
            publicKey: publicKey
        };

        return service;

        function generateJWS() {
            var oHeader = {
                alg: "HS256"
            };
            var sHeader = JSON.stringify(oHeader);
            var sPayload = "aaa";

            var sJWS = KJUR.jws.JWS.sign("HS256", sHeader, sPayload, "616161");

            // var prvKey = KEYUTIL.getKey(sRSAPRV_PKCS8PEM, "password");
            // var sJWS = KJUR.jws.JWS.sign("RS256", JSON.stringify({
            //         alg: "RS256"
            //     }),
            //     sPayload, prvKey);
            return sJWS;
        }

        function verifyJWS(jw) {
            var isValid = KJUR.jws.JWS.verify(jw, publicKey().get(), ["HS256"]);
            return isValid;
        }

        function generateJWT() {
            var oHeader = {
                alg: 'HS256',
                typ: 'JWT'
            };
            var arg = arguments[0];
            var oPayload = {};

            for (var prop in arg) {
                oPayload[prop] = arg[prop];
            }

            var pubKey = oPayload['publicKey'] || publicKey().get();
            var sHeader = JSON.stringify(oHeader);
            var sPayload = JSON.stringify(oPayload);
            var sJWT = KJUR.jws.JWS.sign("HS256", sHeader, sPayload, pubKey);

            // var prvKey = KEYUTIL.getKey(sPKCS8PEM, "password");
            // var sJWT = KJUR.jws.JWS.sign("RS256", sHeader, sPayload, prvKey);
            return sJWT;
        }

        function verifyJWT(jw, pk) {
            var pubKey = pk || publicKey().get();
            var isValid = KJUR.jws.JWS.verifyJWT(jw, pubKey, {
                alg: ['HS256']
            });
            return isValid;
        }

        function getJWT(jw) {
            var sJWT = jw;
            var obj = {};
            obj.header = KJUR.jws.JWS.readSafeJSONString(b64utoutf8(sJWT.split(".")[0]));
            obj.payload = KJUR.jws.JWS.readSafeJSONString(b64utoutf8(sJWT.split(".")[1]));
            return obj;
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
